using System.ComponentModel.DataAnnotations;

namespace ViesClaro.Playwright.Fetch;

/// <summary>
/// Payload de entrada do <c>POST /fetch</c>. <see cref="TimeoutSeconds"/> é
/// opcional — quando omitido, o server usa
/// <c>BrowserPool:DefaultNavTimeoutSeconds</c>. Cap superior aplicado no
/// handler antes de invocar Playwright pra evitar abuso/loop.
/// </summary>
public sealed record FetchRequest
{
    [Required]
    [Url]
    public required string Url { get; init; }

    /// <summary>
    /// Timeout de navegação em segundos. Quando null, usa default da config.
    /// </summary>
    public int? TimeoutSeconds { get; init; }

    /// <summary>
    /// Quando atingir esse estado da página, captura o HTML.
    /// <list type="bullet">
    ///   <item><c>load</c> — DOM + recursos principais carregados</item>
    ///   <item><c>domcontentloaded</c> — DOM montado, sem esperar imagens/CSS</item>
    ///   <item><c>networkidle</c> — sem requests pendentes por 500ms (default; mais lento mas captura SPAs)</item>
    /// </list>
    /// </summary>
    public string? WaitUntil { get; init; }
}

/// <summary>
/// Payload de saída do <c>POST /fetch</c>. <see cref="FinalUrl"/> reflete o
/// destino após redirects/JS navigation; pode diferir de <see cref="Url"/>.
/// </summary>
public sealed record FetchResult
{
    public required string Url { get; init; }
    public required string FinalUrl { get; init; }
    public required int StatusCode { get; init; }
    public required string Html { get; init; }
    public required long DurationMs { get; init; }
    public required DateTimeOffset FetchedAt { get; init; }
}
