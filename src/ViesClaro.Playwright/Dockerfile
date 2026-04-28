# syntax=docker/dockerfile:1.7
#
# Multi-stage build pra viesclaro-playwright-api.
#
# Stage 1 (build): SDK 10 puro pra restore/build/publish.
# Stage 2 (final): imagem oficial mcr.microsoft.com/playwright/dotnet com
#   .NET 10 runtime + Chromium + dependências nativas pré-instaladas.
#
# A imagem final fica grande (~800MB) por causa do Chromium e libs.
# Aceitável dado que o crash isolation do browser justifica o serviço separado.

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

# --- Runtime stage ---
# Imagem oficial Microsoft com Chromium pré-instalado e .NET 10 runtime.
# Tag versionada deve bater com Microsoft.Playwright em Directory.Packages.props.
FROM mcr.microsoft.com/playwright/dotnet:v1.49.0-noble AS final
WORKDIR /app

# Copy do build stage
COPY --from=build /app .

# Config do ambiente
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_GCConserveMemory=9 \
    PLAYWRIGHT_BROWSERS_PATH=/ms-playwright

EXPOSE 8080

ENTRYPOINT ["dotnet", "ViesClaro.Playwright.dll"]
