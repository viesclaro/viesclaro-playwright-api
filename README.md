# viesclaro-playwright-api

Headless browser service (Microsoft.Playwright + Chromium) usado pelo [`viesclaro-orchestrator-api`](https://github.com/viesclaro/viesclaro-orchestrator-api) pra coletar páginas de fontes que:

- são bloqueadas por WAF/Cloudflare/Akamai (TLS fingerprinting do `HttpClient` é detectado), ou
- usam **Next.js/Vue/Angular CSR** onde matérias só aparecem no DOM após hidratação JavaScript.

Roda como serviço standalone (não embedda Chromium no orquestrador) — isolamento de memória, escala independente, crash de browser não derruba o pipeline crítico.

> **Tracker:** [GL-700 — Playwright fetcher para fontes protegidas por WAF](https://linear.app/glashstudios/issue/GL-700)
>
> **Sub-issues:** [GL-701](https://linear.app/glashstudios/issue/GL-701) (este scaffold) · [GL-753](https://linear.app/glashstudios/issue/GL-753) (POST `/fetch`) · [GL-702](https://linear.app/glashstudios/issue/GL-702) (cliente HTTP no orquestrador) · [GL-703](https://linear.app/glashstudios/issue/GL-703) · [GL-704](https://linear.app/glashstudios/issue/GL-704) · [GL-705](https://linear.app/glashstudios/issue/GL-705) · [GL-706](https://linear.app/glashstudios/issue/GL-706)

## Stack

- **.NET 10** Minimal API
- **Microsoft.Playwright 1.49** (Chromium headless)
- **Serilog → Seq** via `ViesClaro.Shared.Observability`
- **Imagem base:** `mcr.microsoft.com/playwright/dotnet:v1.49.0-noble` (Ubuntu 24.04 com Chromium pré-instalado)
- **Deploy:** Dokploy self-hosted, container Docker via webhook GHCR

## Endpoints

```
GET  /                  — service info (status check trivial)
GET  /health/live       — 200 se processo está vivo
GET  /health/ready      — 200 se Chromium responde (instancia context efêmero)
POST /fetch             — em construção, chega em GL-753
```

## Dev setup

```bash
# 1. PAT do GitHub com escopo `read:packages` (Settings → Developer settings)
export GITHUB_USERNAME="seu-usuario"
export GITHUB_TOKEN="ghp_..."

# 2. Restore + build
dotnet restore   # consome ViesClaro.Shared.Observability do GHCR
dotnet build

# 3. Run
dotnet run --project src/ViesClaro.Playwright

# 4. Smoke test
curl http://localhost:8080/health/ready
```

Pra rodar com Playwright local (sem Docker), instala o browser uma vez:
```bash
pwsh src/ViesClaro.Playwright/bin/Debug/net10.0/playwright.ps1 install chromium
```

(O container já vem com Chromium baked-in, então isso só vale localmente.)

## Configuração

`appsettings.json` traz só defaults não-secretos. Em produção (Dokploy), env vars sobrescrevem:

| Env var | Default | Descrição |
|---|---|---|
| `BrowserPool__MaxConcurrency` | 3 | Fetches simultâneos máx |
| `BrowserPool__AcquireTimeoutSeconds` | 60 | Tempo máx esperando vaga no pool |
| `BrowserPool__DefaultNavTimeoutSeconds` | 30 | Timeout default de navegação |
| `BrowserPool__MaxNavTimeoutSeconds` | 60 | Cap superior do timeout que o cliente pode pedir |
| `BrowserPool__UserAgent` | Chrome 131 stub | UA aplicado em cada context |
| `VIESCLARO_PLAYWRIGHT_API_KEY` | — | API key consumida pelo middleware (chega em GL-753) |
| `Seq__ServerUrl` | — | URL do Seq pra logs estruturados |
| `Seq__ApiKey` | — | API key do ingestion endpoint |

## License

MIT
