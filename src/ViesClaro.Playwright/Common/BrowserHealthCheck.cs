using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Playwright;
using ViesClaro.Playwright.BrowserPool;

namespace ViesClaro.Playwright.Common;

/// <summary>
/// Healthcheck do Playwright: abre um <see cref="IBrowserContext"/> efêmero,
/// navega pra <c>about:blank</c>, fecha. Confirma que o singleton está
/// responsivo e que NewContextAsync não está bloqueado por leak/zombie.
/// </summary>
public sealed class BrowserHealthCheck : IHealthCheck
{
    private readonly IBrowserProvider _provider;

    public BrowserHealthCheck(IBrowserProvider provider)
    {
        _provider = provider;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var ctx = await _provider.Browser.NewContextAsync().ConfigureAwait(false);
            var page = await ctx.NewPageAsync().ConfigureAwait(false);
            await page.GotoAsync("about:blank", new PageGotoOptions { Timeout = 5000 }).ConfigureAwait(false);

            return HealthCheckResult.Healthy(
                $"Chromium {_provider.Browser.Version} reachable");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Browser not reachable", ex);
        }
    }
}
