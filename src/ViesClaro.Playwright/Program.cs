using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using ViesClaro.Playwright.BrowserPool;
using ViesClaro.Playwright.Common;
using ViesClaro.Playwright.Fetch;
using ViesClaro.Shared.Observability.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Host.UseViesClaroSerilog(builder.Configuration);

builder.Services.AddOptions<BrowserPoolOptions>()
    .BindConfiguration(BrowserPoolOptions.SectionName);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IBrowserProvider, ChromiumBrowserProvider>();
builder.Services.AddSingleton<IBrowserPool, BrowserPool>();
builder.Services.AddHostedService<BrowserLifecycleHost>();

builder.Services.AddOpenApi();

builder.Services.AddHealthChecks()
    .AddCheck<BrowserHealthCheck>("playwright", tags: ["ready"]);

builder.Services.AddProblemDetails();

var app = builder.Build();

// /openapi/v1.json — JSON OpenAPI nativo do .NET 10 (sem dependência do Swashbuckle).
app.MapOpenApi("/openapi/{documentName}.json");

app.UseViesClaroCorrelationId();
app.UseExceptionHandler();

// Health endpoints ficam SEM API key — Dokploy/Traefik precisam de acesso.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("ready"),
});

app.MapGet("/", () => Results.Ok(new
{
    service = "viesclaro-playwright-api",
    status = "ready",
    fetchEndpoint = "POST /fetch (auth: X-Api-Key)"
}));

// Endpoints protegidos por API key — agrupados pra que /fetch (e futuras
// extensões) compartilhem o middleware sem expor /health.
var protectedRoutes = app.MapGroup("/")
    .AddEndpointFilter<ApiKeyEndpointFilter>();

protectedRoutes.MapPost("/fetch", FetchEndpoint.HandleAsync);

app.Run();

public partial class Program;
