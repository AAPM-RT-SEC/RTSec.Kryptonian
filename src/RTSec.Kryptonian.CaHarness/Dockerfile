# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/RTSec.Kryptonian.CaHarness/RTSec.Kryptonian.CaHarness.csproj src/RTSec.Kryptonian.CaHarness/

RUN dotnet restore src/RTSec.Kryptonian.CaHarness/RTSec.Kryptonian.CaHarness.csproj

COPY src/RTSec.Kryptonian.CaHarness/ src/RTSec.Kryptonian.CaHarness/

WORKDIR /src/src/RTSec.Kryptonian.CaHarness
ARG CACHE_BUST=1
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    curl \
    && rm -rf /var/lib/apt/lists/*

RUN groupadd -r caharness && useradd -r -g caharness caharness

RUN mkdir -p /app/logs && chown -R caharness:caharness /app

COPY --from=build /app/publish .

RUN chown -R caharness:caharness /app

USER caharness

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

EXPOSE 8080

ENTRYPOINT ["dotnet", "RTSec.Kryptonian.CaHarness.dll"]
