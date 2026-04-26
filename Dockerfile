# syntax=docker/dockerfile:1.7

# ============================================================================
# Build stage — SDK completo, restaura + compila + publica
# ============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copia só os arquivos que afetam restore (csproj + props + solution)
# Layer cache do NuGet só invalida quando dependências mudam, não quando código muda
# *.sln* casa tanto o formato antigo .sln quanto o novo .slnx (.NET 10+)
COPY *.sln* Directory.Build.props ./
COPY src/BillFolder.Api/*.csproj            ./src/BillFolder.Api/
COPY src/BillFolder.Application/*.csproj    ./src/BillFolder.Application/
COPY src/BillFolder.Domain/*.csproj         ./src/BillFolder.Domain/
COPY src/BillFolder.Infrastructure/*.csproj ./src/BillFolder.Infrastructure/
COPY tests/BillFolder.Api.Tests/*.csproj    ./tests/BillFolder.Api.Tests/

RUN dotnet restore

# Agora copia o código todo e compila
COPY . .
RUN dotnet publish src/BillFolder.Api \
    -c Release \
    -o /out \
    --no-restore \
    /p:UseAppHost=false

# ============================================================================
# Runtime stage — apenas runtime ASP.NET (imagem ~110MB vs ~750MB do SDK)
# ============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

# Curl pro HEALTHCHECK funcionar
RUN apk add --no-cache curl

# As imagens base do .NET 8+ já incluem um user 'app' não-root.
# Apenas mudamos pra ele aqui.
USER app

COPY --from=build --chown=app:app /out ./

# API escuta em 8080 (HTTP). nginx na frente faz TLS termination.
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_NOLOGO=true

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -fsS http://localhost:8080/v1/health || exit 1

ENTRYPOINT ["dotnet", "BillFolder.Api.dll"]
