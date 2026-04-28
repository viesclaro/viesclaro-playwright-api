using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using ViesClaro.Playwright.BrowserPool;

namespace ViesClaro.Playwright.Fetch;

/// <summary>
/// Handler do <c>POST /fetch</c>. Mapeia exceções para os status codes
/// documentados no contrato em <see cref="FetchRequest"/>:
/// <list type="bullet">
///   <item><c>400</c> — URL malformada ou scheme não-HTTP(S).</item>
///   <item><c>408</c> — timeout de navegação ou aquisição de pool.</item>
///   <item><c>502</c> — erro do browser/site (PlaywrightException).</item>
///   <item><c>500</c> — qualquer outro (não esperado).</item>
/// </list>
/// </summary>
public static class FetchEndpoint
{
    public static async Task<IResult> HandleAsync(
        [FromBody] FetchRequest request,
        IBrowserPool pool,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Url))
        {
            return Results.Problem(
                title: "Invalid request",
                detail: "Body deve conter 'url' não-vazia.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var parsedUri))
        {
            return Results.Problem(
                title: "Invalid URL",
                detail: $"'{request.Url}' não é uma URL absoluta válida.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (parsedUri.Scheme != Uri.UriSchemeHttp && parsedUri.Scheme != Uri.UriSchemeHttps)
        {
            return Results.Problem(
                title: "Invalid URL scheme",
                detail: $"Scheme '{parsedUri.Scheme}' não é suportado. Use http ou https.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var result = await pool.FetchAsync(request, cancellationToken).ConfigureAwait(false);
            return Results.Ok(result);
        }
        catch (TimeoutException ex)
        {
            return Results.Problem(
                title: "Fetch timeout",
                detail: ex.Message,
                statusCode: StatusCodes.Status408RequestTimeout);
        }
        catch (PlaywrightException ex)
        {
            return Results.Problem(
                title: "Browser error",
                detail: ex.Message,
                statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
