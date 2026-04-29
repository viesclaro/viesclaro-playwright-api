# syntax=docker/dockerfile:1.7
#
# Multi-stage build pra viesclaro-playwright-api.
#
# A imagem oficial mcr.microsoft.com/playwright/dotnet ainda usa .NET 8 LTS,
# então optamos por aspnet:10.0 (que bate com TargetFramework do projeto)
# + instalação manual das deps nativas de Chromium + download do browser
# via Microsoft.Playwright CLI (incluído no publish output).
#
# Imagem final ~700-800MB. Aceitável pelo isolamento + paridade de versão.

# --- Build stage ---
ARG DOTNET_VERSION=10.0
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
ARG GITHUB_USERNAME
ARG GITHUB_TOKEN
WORKDIR /src

# 1. Copy package descriptors first pra cache de restore
COPY nuget.config Directory.Build.props Directory.Packages.props ./
COPY src/ViesClaro.Playwright/ViesClaro.Playwright.csproj src/ViesClaro.Playwright/

# 2. Restore — GITHUB_USERNAME e GITHUB_TOKEN vêm via --build-arg no CD
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore src/ViesClaro.Playwright/ViesClaro.Playwright.csproj

# 3. Build + publish
COPY src/ViesClaro.Playwright/ src/ViesClaro.Playwright/
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish src/ViesClaro.Playwright/ViesClaro.Playwright.csproj \
        -c Release \
        -o /app \
        /p:UseAppHost=false

# --- Runtime stage: aspnet:10.0 + Chromium ---
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final
WORKDIR /app

# Lista de deps nativas de Chromium derivada de `playwright install-deps`.
# libasound2t64 é o package em Ubuntu 24 (noble) — versão antiga é libasound2.
# wget+gnupg adicionados pra baixar GPG key do Google e instalar Chrome stable.
RUN apt-get update && apt-get install -y --no-install-recommends \
    wget gnupg ca-certificates \
    libnss3 libnspr4 libdbus-1-3 \
    libatk1.0-0t64 libatk-bridge2.0-0t64 libatspi2.0-0t64 \
    libcups2t64 libdrm2 libxkbcommon0 \
    libxcomposite1 libxdamage1 libxext6 libxfixes3 libxrandr2 \
    libgbm1 libpango-1.0-0 libcairo2 \
    libasound2t64 \
    fonts-liberation libcurl4t64 \
    && rm -rf /var/lib/apt/lists/*

# Google Chrome stable. Detecção de bot via TLS fingerprint (JA3/JA4) é mais
# fácil de quebrar com Chrome real do que Chromium open-source — Cloudflare
# (correio24horas.com.br) e Akamai (noticias.uol.com.br) rejeitavam Chromium
# headless mesmo com mascaramento de navigator.webdriver.
# Baixamos via repo oficial Google + apt instala google-chrome-stable.
# Usamos arch=amd64 explicitamente porque o Dokploy server roda x86_64.
RUN wget -q -O - https://dl-ssl.google.com/linux/linux_signing_key.pub | gpg --dearmor > /etc/apt/trusted.gpg.d/google-chrome.gpg \
    && echo "deb [arch=amd64] http://dl.google.com/linux/chrome/deb/ stable main" > /etc/apt/sources.list.d/google-chrome.list \
    && apt-get update && apt-get install -y --no-install-recommends \
       google-chrome-stable \
    && rm -rf /var/lib/apt/lists/*

# Copy do build stage
COPY --from=build /app .

# Diretório dedicado pro Chromium baixado — sobrevive entre layers/runtime.
# Mantemos Chromium baixado também pra ter fallback se Chrome stable bugar.
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright

# Baixa Chromium via Microsoft.Playwright CLI. O nome exato do assembly varia
# por versão (playwright.dll, Microsoft.Playwright.dll, etc.), então procuramos
# em runtime e usamos o que existir. `dotnet exec` requer runtimeconfig — usamos
# o do app principal pra rodar o entry-point Microsoft.Playwright.Program.Main.
# Usamos chromium como fallback; o default em produção é Chrome stable do apt
# (configurável via env var BrowserPool__BrowserChannel).
RUN if [ -f /app/playwright.dll ]; then \
        dotnet /app/playwright.dll install chromium; \
    elif [ -f /app/Microsoft.Playwright.dll ]; then \
        dotnet exec --runtimeconfig /app/ViesClaro.Playwright.runtimeconfig.json /app/Microsoft.Playwright.dll install chromium; \
    else \
        echo "ERRO: nenhum CLI do Playwright encontrado em /app" && ls -la /app && exit 1; \
    fi

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_GCConserveMemory=9

EXPOSE 8080

ENTRYPOINT ["dotnet", "ViesClaro.Playwright.dll"]
