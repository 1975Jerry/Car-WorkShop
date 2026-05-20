# syntax=docker/dockerfile:1.7

# =============================================================================
# Paint Bull — Azure-targeted Linux container.
#
# Mirrors the local dev runtime exactly:
#   - .NET SDK 10.0.203 (pinned by global.json; rollForward = latestPatch)
#   - ASP.NET Core 10 runtime
#   - net10.0 target for Domain / Application / Infrastructure / Web
#
# Build stage compiles + publishes; runtime stage carries only the ASP.NET
# runtime plus the native libs the app actually exercises (QuestPDF / SkiaSharp
# font stack, curl for the healthcheck).
# =============================================================================

# ---- build stage ------------------------------------------------------------
# SDK 10.0.300 dropped Blazor framework JS extraction from publish output; the
# 200-band pin keeps MapStaticAssets() serving _framework/blazor.web.js via the
# static web asset endpoint manifest. Do not move this off 10.0.2xx without
# re-validating the manifest assertion below.
FROM mcr.microsoft.com/dotnet/sdk:10.0.203 AS build
WORKDIR /src

# Prove which SDK the build layer is actually using. The image tag is meant to
# be immutable, but BuildKit layer cache can keep an older base around — this
# line makes the version visible in every build log, no inference needed.
RUN dotnet --version && dotnet --info | head -20

# Restore in its own layer keyed only on csproj + global.json so source edits
# don't bust the NuGet cache.
COPY global.json ./
COPY src/Workshop.Domain/Workshop.Domain.csproj                 src/Workshop.Domain/
COPY src/Workshop.Application/Workshop.Application.csproj       src/Workshop.Application/
COPY src/Workshop.Infrastructure/Workshop.Infrastructure.csproj src/Workshop.Infrastructure/
COPY src/Workshop.Web/Workshop.Web.csproj                       src/Workshop.Web/
RUN dotnet restore src/Workshop.Web/Workshop.Web.csproj

# Compile + framework-dependent publish (runtime image carries ASP.NET Core 10).
COPY src/ src/
RUN dotnet publish src/Workshop.Web/Workshop.Web.csproj \
        -c Release \
        -o /app/publish \
        --no-restore \
        /p:UseAppHost=false

# Guardrail against the SDK-300 regression: fail the build now if the Blazor
# framework JS route is missing from the static-asset endpoint manifest,
# instead of shipping a container that 404s at /_framework/blazor.web.js.
#
# Substring grep (not JSON-token-anchored) so whitespace / property ordering
# in the manifest can't break the assertion. Diagnostics print every time so
# CI logs always show what the publish actually produced.
RUN set -eu; \
    MANIFEST=/app/publish/Workshop.Web.staticwebassets.endpoints.json; \
    echo "=== publish root ==="; ls -la /app/publish | head -25; \
    if [ ! -f "$MANIFEST" ]; then \
        echo "FATAL: $MANIFEST missing — static asset publishing broke (SDK regression?)"; \
        exit 1; \
    fi; \
    echo "=== manifest size ==="; wc -c "$MANIFEST"; \
    echo "=== first 600 bytes of manifest ==="; head -c 600 "$MANIFEST"; echo; \
    echo "=== unique route prefixes in manifest ==="; \
    grep -oE '"Route":[[:space:]]*"[^/"]+' "$MANIFEST" | sort -u | head -40 || true; \
    echo "=== any 'blazor' substring in manifest? ==="; \
    grep -oiE 'blazor[a-z.]*' "$MANIFEST" | sort -u || echo "(none)"; \
    if ! grep -q '_framework/blazor.web.js' "$MANIFEST"; then \
        echo "FATAL: _framework/blazor.web.js route absent from manifest — SDK pin drifted"; \
        exit 1; \
    fi; \
    echo "=== guardrail passed ==="

# ---- runtime stage ----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl            — HEALTHCHECK probes /health/live
# libfontconfig1  — SkiaSharp font subsystem (QuestPDF quote PDF rendering)
# fonts-dejavu-core — sane fallback fonts so QuestPDF works without bundling extras
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
        curl \
        libfontconfig1 \
        fonts-dejavu-core \
 && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    SEED_DIR=/app/seed

COPY --from=build /app/publish ./
COPY seed/ ./seed/

# IFileStore root + Serilog file sink directory. Both are mounted as named
# volumes in docker-compose; this RUN just guarantees the paths exist when
# running without a volume (e.g. Azure App Service ephemeral filesystem).
RUN mkdir -p /app/wwwroot/uploads /app/logs

EXPOSE 8080

# start-period accounts for EF migrations + SeedRunner (clear lockouts, reset
# passwords to default, hydrate body-panel/service/insurance catalogs) on a
# cold DB. 20s was too tight against a fresh Postgres.
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD curl -fsS http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "Workshop.Web.dll"]
