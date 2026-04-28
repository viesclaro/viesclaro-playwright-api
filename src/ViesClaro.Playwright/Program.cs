using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Playwright;
using ViesClaro.Playwright.BrowserPool;
using ViesClaro.Playwright.Common;
using ViesClaro.Shared.Observability.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Host.UseViesClaroSerilog(builder.Configuration);

builder.Services.AddOptions<BrowserPoolOptions>()
    .BindConfiguration(BrowserPoolOptions.SectionName);

builder.Services.AddSingleton<IBrowserProvider, ChromiumBrowserProvider>();
builder.Services.AddHostedService<BrowserLifecycleHost>();

builder.Services.AddOpenApi();

builder.Services.AddHealthChecks()
    .AddCheck<BrowserHealthCheck>("playwright", tags: ["ready"]);

var app = builder.Build();

// /openapi/v1.json — JSON OpenAPI nativo do .NET 10 (sem dependência do Swashbuckle).
app.MapOpenApi("/openapi/{documentName}.json");

app.UseViesClaroCorrelationId();

// /live — sempre 200 se o processo está vivo (sem checagem de browser)
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });

// /ready — instancia browser context, garante que Chromium responde
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("ready"),
});

// Endpoint /fetch sai em GL-753. Aqui apenas placeholder OPTIONS-like pra
// confirmar que o serviço está de pé e o roteamento está OK.
app.MapGet("/", () => Results.Ok(new
{
    service = "viesclaro-playwright-api",
    status = "ready",
    fetchEndpoint = "POST /fetch (em construção — ver GL-753)"
}));

app.Run();

public partial class Program;
