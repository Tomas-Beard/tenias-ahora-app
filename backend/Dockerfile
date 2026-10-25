# ==============================================================================
# Tenis Ahora (IEEE SRS-830) — Dockerfile Multi-Stage Optimizado (.NET 8)
# Diseñado para despliegue ligero y seguro en VPS Ubuntu 22.04 / 24.04 LTS
# ==============================================================================

# ------------------------------------------------------------------------------
# ETAPA 1: Build & Publish (SDK .NET 8 Alpine)
# ------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copiar archivo de proyecto primero para maximizar caché de capas en restore
COPY ["TenisAhora.Api.csproj", "./"]
RUN dotnet restore "TenisAhora.Api.csproj" --runtime linux-musl-x64

# Copiar código fuente por capas de Clean Architecture (34 entidades TPT)
COPY ["Domain/", "Domain/"]
COPY ["Application/", "Application/"]
COPY ["Infrastructure/", "Infrastructure/"]
COPY ["Api/", "Api/"]
COPY ["Program.cs", "appsettings.json", "./"]

# Publicar en modo Release sin AppHost nativo redundante para reducir tamaño final
RUN dotnet publish "TenisAhora.Api.csproj" \
    -c Release \
    -r linux-musl-x64 \
    --self-contained false \
    --no-restore \
    -o /app/publish \
    /p:UseAppHost=false \
    /p:DebugType=None \
    /p:DebugSymbols=false

# ------------------------------------------------------------------------------
# ETAPA 2: Runtime Ligero de Producción (ASP.NET Core 8.0 Alpine)
# ------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app

# Instalar soporte de internacionalización (es-AR), zona horaria y curl para healthcheck
RUN apk add --no-cache \
    icu-libs \
    icu-data-full \
    tzdata \
    curl \
    ca-certificates \
    && cp /usr/share/zoneinfo/America/Argentina/Buenos_Aires /etc/localtime \
    && echo "America/Argentina/Buenos_Aires" > /etc/timezone

# Crear usuario y grupo sin privilegios (non-root) por seguridad en producción
RUN addgroup -g 10001 -S tenisgroup \
    && adduser -u 10001 -S appuser -G tenisgroup \
    && chown -R appuser:tenisgroup /app

# Variables de entorno optimizadas para VPS de recursos acotados
ENV ASPNETCORE_URLS=http://+:5090 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    LC_ALL=es_AR.UTF-8 \
    LANG=es_AR.UTF-8 \
    TZ=America/Argentina/Buenos_Aires \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_gcServer=0

# Copiar artefactos compilados desde la etapa build
COPY --from=build --chown=appuser:tenisgroup /app/publish ./

# Ejecutar como usuario no-root
USER appuser

# Puerto interno expuesto (Nginx enruta tráfico HTTPS 443 -> 127.0.0.1:5090)
EXPOSE 5090

# Verificación periódica de salud contra el endpoint /api/health
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -fsS http://127.0.0.1:5090/api/health || exit 1

ENTRYPOINT ["dotnet", "TenisAhora.Api.dll"]
