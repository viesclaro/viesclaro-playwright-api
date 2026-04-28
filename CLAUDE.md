# viesclaro-playwright-api — Project Rules

## Propósito

Serviço .NET 10 standalone que provê **fetch de páginas web via Chromium headless** pro `viesclaro-orchestrator-api`. Necessário pra:

1. Fontes bloqueadas por WAF/Cloudflare/Akamai (TLS fingerprinting JA3/JA4 do `.NET HttpClient` é detectado e devolve 403 antes de inspecionar headers).
2. Sites SPA/CSR (Next.js/Vue/Angular) cujo HTML inicial não traz matérias — só renderizam via JS após hidratação.

**Não orquestra nada** — só recebe URL, navega, retorna HTML. Toda decisão (qual fonte usa browser, retries, schedule) fica no orquestrador.

- **Linear:** [Sub-épico B.3.2 — GL-700](https://linear.app/glashstudios/issue/GL-700)
- **GitHub:** https://github.com/viesclaro/viesclaro-playwright-api
- **Epic-pai:** [GL-585 — Refundação Plataforma Viés Claro v2](https://linear.app/glashstudios/issue/GL-585)

## Stack

- **.NET 10** Minimal API
- **Microsoft.Playwright 1.49** (Chromium headless)
- **Serilog → Seq** via `ViesClaro.Shared.Observability` (NuGet do GHCR)
- **Central Package Management** (`Directory.Packages.props`)
- **Imagem base:** `mcr.microsoft.com/playwright/dotnet:v1.49.0-noble` — Ubuntu 24 + .NET 10 + Chromium pré-instalado
- **Deploy:** Dokploy self-hosted (sem domínio público; rede interna entre containers)

## Estrutura esperada

```
viesclaro-playwright-api/
├── src/
│   └── ViesClaro.Playwright/
│       ├── BrowserPool/                  # Provider, Lifecycle, Options
│       ├── Common/                       # Healthcheck, middleware, etc.
│       ├── Fetch/                        # Endpoint POST /fetch — chega em GL-753
│       ├── Program.cs
│       ├── appsettings.json              # só não-secretos (max concurrency, timeouts, UA)
│       ├── appsettings.Local.json        # gitignored
│       └── Dockerfile
├── tests/
│   └── ViesClaro.Playwright.Tests/       # xUnit, NSubstitute, FluentAssertions
├── .github/workflows/cd.yml              # build → push GHCR → webhook Dokploy
├── Directory.Packages.props
├── Directory.Build.props
├── nuget.config                          # GHCR source com %GITHUB_TOKEN%
├── ViesClaro.Playwright.sln
└── README.md
```

## Convenções

- **Nenhuma escrita em DB.** Esse serviço é stateless — não tem DbContext, não toca Postgres.
- **Sem RabbitMQ.** Comunicação é HTTP síncrona via JSON. Cliente do orquestrador chama `POST /fetch` e bloqueia até receber HTML.
- **Browser singleton.** Um único `IBrowser` (Chromium) vive durante o ciclo do processo, criado eagerly no `BrowserLifecycleHost.StartAsync`. Cada request abre um `IBrowserContext` isolado (cookies/storage não vazam entre fontes) e fecha ao final.
- **Concurrency cap via SemaphoreSlim.** `BrowserPool:MaxConcurrency` (default 3) limita fetches simultâneos pra preservar memória — Chromium consome ~250MB/contexto.
- **Auth interna.** `POST /fetch` exige header `X-Api-Key` que bate com env var `VIESCLARO_PLAYWRIGHT_API_KEY`. `/health/*` continua público pra Dokploy/Traefik conseguirem inspecionar.
- **Healthcheck rico.** `/health/ready` instancia um `IBrowserContext` efêmero contra `about:blank` — confirma que Chromium responde, não só que o processo está vivo.
- **Logs estruturados.** Serilog com correlation ID via `UseViesClaroCorrelationId`. Campos chave: `Url`, `DurationMs`, `StatusCode`, `WaitMs` (semaphore queue).
- **Sem Swagger em produção.** UI só em `ASPNETCORE_ENVIRONMENT=Development|Local`. JSON OpenAPI continua exposto em `/openapi/v1.json` pra ferramentas.

## Secrets e config

**Zero secret em `appsettings.json`.** Apenas valores não-secretos (max concurrency, timeouts, UA). Secrets vêm de env vars do Dokploy:

- `VIESCLARO_PLAYWRIGHT_API_KEY` — API key consumida pelo middleware do `/fetch`
- `Seq__ServerUrl`, `Seq__ApiKey` — observability

GitHub Secrets (pipeline): `DOKPLOY_URL`, `DOKPLOY_API_KEY`, `DOKPLOY_PLAYWRIGHT_APP_ID` (variable), `GHCR_TOKEN` (já automático via `GITHUB_TOKEN` no Actions).

## Dev setup

```bash
# 1. PAT GitHub com escopo read:packages
export GITHUB_USERNAME="seu-usuario"
export GITHUB_TOKEN="ghp_..."

# 2. Restore + build
dotnet restore
dotnet build

# 3. Instalar Chromium localmente (uma vez)
pwsh src/ViesClaro.Playwright/bin/Debug/net10.0/playwright.ps1 install chromium

# 4. Run
dotnet run --project src/ViesClaro.Playwright

# Smoke test
curl http://localhost:8080/         # info do serviço
curl http://localhost:8080/health/ready   # confirma Chromium reachable
```

## Linear (sub-issues)

Ordem de execução do épico [GL-700](https://linear.app/glashstudios/issue/GL-700):

1. [GL-701](https://linear.app/glashstudios/issue/GL-701) — Setup repo + scaffold + CD (este commit inicial)
2. [GL-753](https://linear.app/glashstudios/issue/GL-753) — Server: POST `/fetch` + browser pool + auth
3. [GL-702](https://linear.app/glashstudios/issue/GL-702) — Cliente HTTP no orquestrador
4. [GL-703](https://linear.app/glashstudios/issue/GL-703) — Roteamento `Source.RequireBrowser`
5. [GL-704](https://linear.app/glashstudios/issue/GL-704) — Observability + concurrency limits
6. [GL-705](https://linear.app/glashstudios/issue/GL-705) — Admin UI: editar `Source.RequireBrowser`
7. [GL-706](https://linear.app/glashstudios/issue/GL-706) — Reativação piloto (Forbes, Correio, UOL, O Popular, Jornal Daqui)

## Do NOT

- Do **NOT** escrever nada em DB. Este serviço é stateless por design.
- Do **NOT** consumir RabbitMQ. Comunicação é HTTP sync.
- Do **NOT** referenciar `ViesClaro.Schema` no `.csproj`. Não tem entity nenhuma aqui.
- Do **NOT** rodar Chromium com `Headless = false` em produção — leak de RAM e tela do display.
- Do **NOT** reusar o mesmo `IBrowserContext` entre requests — cookies/storage vazariam entre fontes.
- Do **NOT** commitar secrets. `grep -r "ApiKey\|password\|secret" --include="*.json"` deve voltar vazio.
- Do **NOT** subir versão do `Microsoft.Playwright` sem atualizar a tag da imagem base no Dockerfile (precisam estar sincronizados — divergência leva a Chromium not found).
