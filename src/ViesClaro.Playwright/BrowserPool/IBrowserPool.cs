using ViesClaro.Playwright.Fetch;

namespace ViesClaro.Playwright.BrowserPool;

/// <summary>
/// Pool de <c>IBrowserContext</c> isolados sobre o singleton <see cref="IBrowserProvider"/>.
/// Limita concorrência via semaphore + isola cookies/storage por request.
/// </summary>
public interface IBrowserPool
{
    /// <summary>
    /// Executa o fetch; lança <see cref="TimeoutException"/> se o semaphore
    /// não liberar dentro de <c>BrowserPool:AcquireTimeoutSeconds</c>, ou se
    /// a navegação Playwright estourar <see cref="FetchRequest.TimeoutSeconds"/>.
    /// Lança <see cref="Microsoft.Playwright.PlaywrightException"/> em outros
    /// erros do browser/rede.
    /// </summary>
    Task<FetchResult> FetchAsync(FetchRequest request, CancellationToken cancellationToken);
}
