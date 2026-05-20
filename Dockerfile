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
