using Microsoft.Playwright;

namespace ViesClaro.Playwright.BrowserPool;

/// <summary>
/// Provê o singleton <see cref="IBrowser"/> usado em todos os fetches.
/// Inicializa Chromium uma única vez no startup; consumidores chamam
/// <see cref="Browser"/> a cada request — cada fetch abre um
/// <see cref="IBrowserContext"/> isolado pra não vazar cookies entre fontes.
/// </summary>
public interface IBrowserProvider
{
    IBrowser Browser { get; }

    Task EnsureStartedAsync(CancellationToken cancellationToken);

    Task ShutdownAsync(CancellationToken cancellationToken);
}
