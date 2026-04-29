namespace ViesClaro.Playwright.BrowserPool;

/// <summary>
/// Configuração do pool de browser contexts. Bind via section
/// <c>BrowserPool</c> em <c>appsettings.json</c> + env vars do Dokploy.
/// </summary>
public sealed class BrowserPoolOptions
{
    public const string SectionName = "BrowserPool";

    /// <summary>
    /// Quantidade máxima de fetches simultâneos (semaphore size). Default 5 cobre
    /// 5 fontes piloto + folga pra DrainLinks pumping artigos individuais. RAM
    /// estimada: ~250MB/contexto × 5 = ~1.25GB pico (config Dokploy: limite 2GB).
    /// </summary>
    public int MaxConcurrency { get; set; } = 5;

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

    /// <summary>
    /// Canal do browser que Playwright vai usar:
    /// <list type="bullet">
    ///   <item><c>chrome</c> (default) — Google Chrome stable, instalado via apt no Dockerfile.
    ///   TLS fingerprint (JA3/JA4) coincide com Chrome real, passa em mais checks de WAF
    ///   (Cloudflare, Akamai). Caso real: correio/uol rejeitam Chromium headless mesmo
    ///   mascarado.</item>
    ///   <item><c>chromium</c> — Chromium open-source baixado pelo Playwright CLI.
    ///   Mais leve mas detectável em alguns WAFs.</item>
    ///   <item>Vazio ou null — Playwright usa o Chromium baixado, sem nenhum channel
    ///   específico. Mesmo comportamento que <c>chromium</c>.</item>
    /// </list>
    /// Override via env var Dokploy <c>BrowserPool__BrowserChannel</c>.
    /// </summary>
    public string? BrowserChannel { get; set; } = "chrome";
}
