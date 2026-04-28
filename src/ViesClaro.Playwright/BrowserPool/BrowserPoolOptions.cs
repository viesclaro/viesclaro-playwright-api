namespace ViesClaro.Playwright.BrowserPool;

/// <summary>
/// Configuração do pool de browser contexts. Bind via section
/// <c>BrowserPool</c> em <c>appsettings.json</c> + env vars do Dokploy.
/// </summary>
public sealed class BrowserPoolOptions
{
    public const string SectionName = "BrowserPool";

    /// <summary>Quantidade máxima de fetches simultâneos (semaphore size).</summary>
    public int MaxConcurrency { get; set; } = 3;

    /// <summary>Tempo máximo aguardando vaga no semaphore antes de retornar 503.</summary>
    public int AcquireTimeoutSeconds { get; set; } = 60;

    /// <summary>Timeout default de navegação quando o cliente não especifica.</summary>
    public int DefaultNavTimeoutSeconds { get; set; } = 30;

    /// <summary>Cap superior do timeout de navegação solicitado pelo cliente.</summary>
    public int MaxNavTimeoutSeconds { get; set; } = 60;

    /// <summary>Locale aplicado em cada BrowserContext (Accept-Language coerente).</summary>
    public string Locale { get; set; } = "pt-BR";

    public int ViewportWidth { get; set; } = 1920;
    public int ViewportHeight { get; set; } = 1080;

    /// <summary>User-Agent dos contexts. Atualizar periodicamente pra não virar fingerprint reconhecível.</summary>
    public string UserAgent { get; set; } =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
}
