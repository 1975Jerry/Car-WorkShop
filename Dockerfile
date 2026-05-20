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

# Repair publish output. .NET SDK 10.0.300 regressed: blazor.web.js / blazor.server.js
# no longer get copied into publish/wwwroot/_framework/, which makes /_framework/* 404
# in production (MapStaticAssets auto-discovers from wwwroot — files not on disk = no
# endpoint). The files still exist elsewhere in the SDK toolchain (bin output and
# package caches), so we locate them and forward them into publish.
RUN echo "=== dotnet --info ===" && dotnet --info | head -20 \
 && echo "=== publish/wwwroot before repair ===" && ls -la /app/publish/wwwroot \
 && echo "=== framework JS locations in the SDK image ===" \
 && (find /src /usr/share/dotnet /root \( -name 'blazor.web.js' -o -name 'blazor.server.js' \) -type f 2>/dev/null || true) \
 && mkdir -p /app/publish/wwwroot/_framework \
 && for f in blazor.web.js blazor.server.js; do \
      src="$(find /src /usr/share/dotnet /root -name "$f" -type f 2>/dev/null | head -1)"; \
      if [ -n "$src" ]; then \
        cp "$src" "/app/publish/wwwroot/_framework/$f"; \
        echo "forwarded $src -> /app/publish/wwwroot/_framework/$f"; \
      else \
        echo "FATAL: $f not found in SDK image"; exit 1; \
      fi; \
    done \
 && echo "=== publish/wwwroot/_framework after repair ===" \
 && ls -la /app/publish/wwwroot/_framework/

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
