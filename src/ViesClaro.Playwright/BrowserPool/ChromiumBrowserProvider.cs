using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace ViesClaro.Playwright.BrowserPool;

/// <summary>
/// <see cref="IBrowserProvider"/> baseado em Chromium headless. Cria a
/// instância única no startup via <see cref="BrowserLifecycleHost"/> pra que
/// o cold-start fique fora do hot path da primeira request.
/// </summary>
public sealed partial class ChromiumBrowserProvider : IBrowserProvider, IAsyncDisposable
{
    private readonly ILogger<ChromiumBrowserProvider> _logger;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public ChromiumBrowserProvider(ILogger<ChromiumBrowserProvider> logger)
    {
        _logger = logger;
    }

    public IBrowser Browser => _browser
        ?? throw new InvalidOperationException(
            "Browser ainda não foi iniciado. Espere o BrowserLifecycleHost completar o startup.");

    public async Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        if (_browser is not null) return;

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_browser is not null) return;

            _playwright = await Microsoft.Playwright.Playwright.CreateAsync().ConfigureAwait(false);
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = ["--disable-blink-features=AutomationControlled"],
            }).ConfigureAwait(false);

            LogReady(_browser.Version);
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task ShutdownAsync(CancellationToken cancellationToken)
    {
        if (_browser is not null)
        {
            try
            {
                await _browser.CloseAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogShutdownError(ex);
            }
            _browser = null;
        }

        _playwright?.Dispose();
        _playwright = null;
    }

    public async ValueTask DisposeAsync()
    {
        await ShutdownAsync(CancellationToken.None).ConfigureAwait(false);
        _initLock.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Playwright ready: Chromium {ChromiumVersion}")]
    private partial void LogReady(string chromiumVersion);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Browser shutdown threw — pode deixar Chromium zombie até container restart")]
    private partial void LogShutdownError(Exception ex);
}
