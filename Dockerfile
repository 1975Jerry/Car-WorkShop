# syntax=docker/dockerfile:1.7

# ---- build stage ----------------------------------------------------------
# Pinned to SDK feature band 200. The floating sdk:10.0 tag now resolves to 10.0.300,
# which regressed Blazor framework asset publishing — blazor.web.js / blazor.server.js
# no longer materialise into publish/wwwroot/_framework/ at all (the JS lives as an
# embedded resource inside Microsoft.AspNetCore.Components.Web.dll and is no longer
# extracted). 10.0.203 still emits the physical files, so /_framework/blazor.web.js
# is served on Azure. Revisit once an MSBuild opt-in or 10.0.300+ fix exists.
FROM mcr.microsoft.com/dotnet/sdk:10.0.203 AS build
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

# Sanity check the SDK pin worked and publish produced the framework JS we depend on.
RUN echo "=== dotnet --info ===" && dotnet --info | head -8 \
 && echo "=== publish/wwwroot/_framework ===" \
 && ls -la /app/publish/wwwroot/_framework \
 && test -f /app/publish/wwwroot/_framework/blazor.web.js \
 && test -f /app/publish/Workshop.Web.staticwebassets.endpoints.json

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
