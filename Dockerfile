# syntax=docker/dockerfile:1.7

# ---- build stage ----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files first for layer-cached restore.
COPY src/Workshop.Domain/Workshop.Domain.csproj         src/Workshop.Domain/
COPY src/Workshop.Application/Workshop.Application.csproj src/Workshop.Application/
COPY src/Workshop.Infrastructure/Workshop.Infrastructure.csproj src/Workshop.Infrastructure/
COPY src/Workshop.Web/Workshop.Web.csproj               src/Workshop.Web/
RUN dotnet restore src/Workshop.Web/Workshop.Web.csproj

# Bring in the source and publish.
COPY src/ src/
RUN dotnet publish src/Workshop.Web/Workshop.Web.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# Diagnose the Azure "/_framework/blazor.web.js 404" issue: dump the publish layout so
# the build log shows exactly what was produced. Then fail loudly if the framework JS
# is missing — turns a silent runtime 404 into a visible CI failure.
RUN echo "=== dotnet --info ===" && dotnet --info \
 && echo "=== publish root ===" && ls -la /app/publish | head -50 \
 && echo "=== publish/wwwroot ===" && ls -la /app/publish/wwwroot \
 && echo "=== publish/wwwroot/_framework (or absent) ===" \
 && (ls -la /app/publish/wwwroot/_framework 2>&1 || true) \
 && echo "=== blazor.web.js in publish anywhere? ===" \
 && (find /app/publish -name 'blazor.web*' -o -name 'staticwebassets*' 2>&1 || true) \
 && echo "=== checking required artifacts ===" \
 && test -f /app/publish/Workshop.Web.staticwebassets.endpoints.json \
 && test -f /app/publish/wwwroot/_framework/blazor.web.js

# ---- runtime stage --------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    SEED_DIR=/app/seed \
    DOTNET_RUNNING_IN_CONTAINER=true

COPY --from=build /app/publish ./
COPY seed/ ./seed/

# wwwroot/uploads is the IFileStore root; make sure it exists and is writable.
RUN mkdir -p /app/wwwroot/uploads /app/logs

EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl -f http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "Workshop.Web.dll"]
