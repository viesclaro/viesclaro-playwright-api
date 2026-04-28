using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using ViesClaro.Playwright.Fetch;

namespace ViesClaro.Playwright.BrowserPool;

/// <summary>
/// Implementação default do <see cref="IBrowserPool"/>. Cada request abre um
/// <c>IBrowserContext</c> efêmero (cookies/storage isolados → sem leakage entre
/// fontes), navega via Playwright e fecha. Concorrência limitada por
/// <see cref="SemaphoreSlim"/> dimensionado por <see cref="BrowserPoolOptions.MaxConcurrency"/>.
/// </summary>
public sealed partial class BrowserPool : IBrowserPool, IDisposable
{
    private const long SaturationWarningThresholdMs = 5_000;

    private readonly IBrowserProvider _browserProvider;
    private readonly BrowserPoolOptions _options;
    private readonly ILogger<BrowserPool> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _semaphore;

    public BrowserPool(
        IBrowserProvider browserProvider,
        IOptions<BrowserPoolOptions> options,
        TimeProvider timeProvider,
        ILogger<BrowserPool> logger)
    {
        _browserProvider = browserProvider;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
        _semaphore = new SemaphoreSlim(_options.MaxConcurrency, _options.MaxConcurrency);
    }

    public async Task<FetchResult> FetchAsync(FetchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var navTimeoutSeconds = ClampTimeout(request.TimeoutSeconds);
        var (waitUntil, postLoadDelayMs) = ParseStrategy(request.WaitUntil);

        var waitSw = Stopwatch.StartNew();
        var acquireTimeout = TimeSpan.FromSeconds(_options.AcquireTimeoutSeconds);
        var acquired = await _semaphore.WaitAsync(acquireTimeout, cancellationToken).ConfigureAwait(false);
        waitSw.Stop();

        if (!acquired)
        {
            LogPoolSaturationTimeout(request.Url, waitSw.ElapsedMilliseconds);
            throw new TimeoutException(
                $"Browser pool saturado: aquisição não completou em {acquireTimeout.TotalSeconds:F0}s. Cliente pode retentar.");
        }

        if (waitSw.ElapsedMilliseconds >= SaturationWarningThresholdMs)
        {
            LogPoolSaturationSlow(request.Url, waitSw.ElapsedMilliseconds);
        }

        try
        {
            return await FetchInternalAsync(request, navTimeoutSeconds, waitUntil, postLoadDelayMs, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<FetchResult> FetchInternalAsync(
        FetchRequest request,
        int navTimeoutSeconds,
        WaitUntilState waitUntil,
        int postLoadDelayMs,
        CancellationToken cancellationToken)
    {
        LogFetchStarted(request.Url, navTimeoutSeconds);

        var sw = Stopwatch.StartNew();
        IBrowserContext? context = null;
        try
        {
            context = await _browserProvider.Browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = _options.UserAgent,
                Locale = _options.Locale,
                ViewportSize = new ViewportSize
                {
                    Width = _options.ViewportWidth,
                    Height = _options.ViewportHeight,
                },
                ExtraHTTPHeaders = new Dictionary<string, string>
                {
                    ["Accept-Language"] = "pt-BR,pt;q=0.9,en;q=0.8",
                },
            }).ConfigureAwait(false);

            // Mascara fingerprints óbvios de Chromium headless. Cobre os checks
            // mais comuns (navigator.webdriver, navigator.plugins.length=0,
            // navigator.languages vazio) que sites tipo Cloudflare/Akamai usam
            // pra rejeitar bots ANTES do JS challenge real. Caso real (28-04):
            // correio24horas.com.br e noticias.uol.com.br retornavam 403 mesmo
            // com Chromium real headless até este script ser injetado.
            await context.AddInitScriptAsync(@"
                Object.defineProperty(navigator, 'webdriver', { get: () => undefined });
                Object.defineProperty(navigator, 'plugins', { get: () => [1, 2, 3, 4, 5] });
                Object.defineProperty(navigator, 'languages', { get: () => ['pt-BR', 'pt', 'en'] });
                window.chrome = { runtime: {} };
            ").ConfigureAwait(false);

            var page = await context.NewPageAsync().ConfigureAwait(false);
            var response = await page.GotoAsync(request.Url, new PageGotoOptions
            {
                Timeout = navTimeoutSeconds * 1000f,
                WaitUntil = waitUntil,
            }).ConfigureAwait(false);

            // Estratégia "hydrate": espera N segundos depois de DOMContentLoaded
            // pra hidratação JS terminar (React/Next.js fazem fetch lazy pós-mount).
            // Mais determinístico que NetworkIdle que nunca estabiliza em sites com
            // analytics/ads polling contínuo (caso real: o-popular timed out 60s).
            if (postLoadDelayMs > 0)
            {
                LogPostLoadWait(request.Url, postLoadDelayMs);
                await page.WaitForTimeoutAsync(postLoadDelayMs).ConfigureAwait(false);
            }

            var html = await page.ContentAsync().ConfigureAwait(false);
            sw.Stop();

            var statusCode = response?.Status ?? 0;
            LogFetchCompleted(request.Url, statusCode, sw.ElapsedMilliseconds);

            return new FetchResult
            {
                Url = request.Url,
                FinalUrl = page.Url,
                StatusCode = statusCode,
                Html = html,
                DurationMs = sw.ElapsedMilliseconds,
                FetchedAt = _timeProvider.GetUtcNow(),
            };
        }
        catch (TimeoutException ex)
        {
            sw.Stop();
            LogFetchTimeout(request.Url, sw.ElapsedMilliseconds);
            throw new TimeoutException(
                $"Navegação para {request.Url} estourou {navTimeoutSeconds}s.", ex);
        }
        catch (PlaywrightException ex)
        {
            sw.Stop();
            LogFetchPlaywrightFailed(request.Url, sw.ElapsedMilliseconds, ex.Message);
            throw;
        }
        finally
        {
            if (context is not null)
            {
                try
                {
                    await context.CloseAsync().ConfigureAwait(false);
                }
                catch (Exception closeEx)
                {
                    LogContextCloseFailed(request.Url, closeEx.Message);
                }
            }
        }
    }

    private int ClampTimeout(int? requested)
    {
        var value = requested ?? _options.DefaultNavTimeoutSeconds;
        return Math.Clamp(value, 1, _options.MaxNavTimeoutSeconds);
    }

    /// <summary>
    /// Mapeia a estratégia textual do cliente em <c>(WaitUntilState, postLoadDelayMs)</c>:
    /// <list type="bullet">
    ///   <item><c>domcontentloaded</c> (default) — DOM montado, sem delay. ~1-3s.</item>
    ///   <item><c>load</c> — aguarda <c>window.load</c> (CSS/imgs primárias). ~2-5s.</item>
    ///   <item><c>networkidle</c> — aguarda 500ms sem requests. Sites com analytics
    ///   contínuo nunca estabilizam — usar com cuidado.</item>
    ///   <item><c>hydrate</c> — DOMContentLoaded + delay de 5s pra hidratação JS
    ///   terminar (React/Next.js fazem fetch lazy pós-mount). Determinístico,
    ///   recomendado pra SPAs que precisam mostrar conteúdo dinâmico.</item>
    /// </list>
    /// </summary>
    private const int HydratePostLoadDelayMs = 5_000;
    private static (WaitUntilState waitUntil, int postLoadDelayMs) ParseStrategy(string? raw) =>
        raw?.ToLowerInvariant() switch
        {
            "load" => (WaitUntilState.Load, 0),
            "domcontentloaded" or null => (WaitUntilState.DOMContentLoaded, 0),
            "networkidle" => (WaitUntilState.NetworkIdle, 0),
            "hydrate" => (WaitUntilState.DOMContentLoaded, HydratePostLoadDelayMs),
            _ => (WaitUntilState.DOMContentLoaded, 0),
        };

    public void Dispose() => _semaphore.Dispose();

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Fetch started {Url} timeoutSeconds={TimeoutSeconds}")]
    private partial void LogFetchStarted(string url, int timeoutSeconds);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Fetch completed {Url} statusCode={StatusCode} durationMs={DurationMs}")]
    private partial void LogFetchCompleted(string url, int statusCode, long durationMs);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Fetch timeout {Url} after {DurationMs}ms")]
    private partial void LogFetchTimeout(string url, long durationMs);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Fetch failed {Url} after {DurationMs}ms reason={Reason}")]
    private partial void LogFetchPlaywrightFailed(string url, long durationMs, string reason);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Context.CloseAsync threw for {Url} reason={Reason} — pode deixar resources zumbi até reciclagem do browser")]
    private partial void LogContextCloseFailed(string url, string reason);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "BrowserPool saturation — waited {WaitMs}ms for slot before fetching {Url}")]
    private partial void LogPoolSaturationSlow(string url, long waitMs);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "BrowserPool saturation timeout — falhou em adquirir slot em {WaitMs}ms para {Url}")]
    private partial void LogPoolSaturationTimeout(string url, long waitMs);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Hydration wait {DelayMs}ms after DOMContentLoaded for {Url}")]
    private partial void LogPostLoadWait(string url, int delayMs);
}
