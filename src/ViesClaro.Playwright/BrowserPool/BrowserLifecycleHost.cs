using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ViesClaro.Playwright.BrowserPool;

/// <summary>
/// <see cref="IHostedService"/> que inicia o Chromium no boot do container e
/// faz shutdown gracioso no <see cref="IHostApplicationLifetime.ApplicationStopping"/>.
///
/// <para>
/// Eager start (em vez de lazy on first request) garante: (1) cold-start
/// determinístico; (2) <c>/health/ready</c> só fica verde quando o browser
/// realmente subiu; (3) crash de inicialização derruba o container imediatamente
/// em vez de mascarar como timeout no primeiro fetch.
/// </para>
/// </summary>
public sealed partial class BrowserLifecycleHost : IHostedService
{
    private readonly IBrowserProvider _provider;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<BrowserLifecycleHost> _logger;

    public BrowserLifecycleHost(
        IBrowserProvider provider,
        IHostApplicationLifetime lifetime,
        ILogger<BrowserLifecycleHost> logger)
    {
        _provider = provider;
        _lifetime = lifetime;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _provider.EnsureStartedAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogStartupFailed(ex);
            // Falha no startup é fatal — sem browser, o serviço não tem propósito.
            // Deixa o container morrer pra Dokploy reagir (restart policy).
            _lifetime.StopApplication();
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _provider.ShutdownAsync(cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Critical,
        Message = "Falha ao iniciar Chromium — container vai parar")]
    private partial void LogStartupFailed(Exception ex);
}
