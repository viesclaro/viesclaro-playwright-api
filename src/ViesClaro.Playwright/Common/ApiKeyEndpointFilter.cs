using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ViesClaro.Playwright.Common;

/// <summary>
/// Endpoint filter que valida o header <c>X-Api-Key</c> contra o env var
/// <c>VIESCLARO_PLAYWRIGHT_API_KEY</c>. Comparação timing-safe via
/// <see cref="CryptographicOperations.FixedTimeEquals(System.ReadOnlySpan{byte}, System.ReadOnlySpan{byte})"/>.
///
/// <para>
/// Aplicado seletivamente via <c>group.AddEndpointFilter&lt;ApiKeyEndpointFilter&gt;()</c>.
/// <c>/health/*</c> ficam públicos (sem o filter) pra Traefik/Dokploy.
/// </para>
/// </summary>
public sealed partial class ApiKeyEndpointFilter : IEndpointFilter
{
    public const string HeaderName = "X-Api-Key";
    public const string EnvVarName = "VIESCLARO_PLAYWRIGHT_API_KEY";

    private readonly byte[] _expectedKeyBytes;
    private readonly ILogger<ApiKeyEndpointFilter> _logger;

    public ApiKeyEndpointFilter(IConfiguration configuration, ILogger<ApiKeyEndpointFilter> logger)
    {
        _logger = logger;
        var key = configuration[EnvVarName];
        if (string.IsNullOrWhiteSpace(key))
        {
            // Falhar fast no startup é melhor que deixar /fetch rodar sem auth.
            throw new InvalidOperationException(
                $"Env var '{EnvVarName}' não configurada. Endpoints protegidos não podem subir sem ela.");
        }
        _expectedKeyBytes = Encoding.UTF8.GetBytes(key);
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;

        if (!http.Request.Headers.TryGetValue(HeaderName, out var header) || header.Count == 0)
        {
            return Reject(http, "missing");
        }

        var providedKey = header.ToString();
        if (string.IsNullOrEmpty(providedKey))
        {
            return Reject(http, "empty");
        }

        var providedBytes = Encoding.UTF8.GetBytes(providedKey);
        if (!CryptographicOperations.FixedTimeEquals(providedBytes, _expectedKeyBytes))
        {
            return Reject(http, "mismatch");
        }

        return await next(context);
    }

    private IResult Reject(HttpContext http, string reason)
    {
        LogReject(reason, http.Request.Path);
        return Results.Unauthorized();
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "API key rejected (reason={Reason}) for path {Path}")]
    private partial void LogReject(string reason, string path);
}
