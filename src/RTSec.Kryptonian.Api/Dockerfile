# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for restore (all src projects)
COPY src/RTSec.Kryptonian.Shared/RTSec.Kryptonian.Shared.csproj src/RTSec.Kryptonian.Shared/
COPY src/RTSec.Kryptonian.Domain/RTSec.Kryptonian.Domain.csproj src/RTSec.Kryptonian.Domain/
COPY src/RTSec.Kryptonian.Application/RTSec.Kryptonian.Application.csproj src/RTSec.Kryptonian.Application/
COPY src/RTSec.Kryptonian.Infrastructure/RTSec.Kryptonian.Infrastructure.csproj src/RTSec.Kryptonian.Infrastructure/
COPY src/RTSec.Kryptonian.Api/RTSec.Kryptonian.Api.csproj src/RTSec.Kryptonian.Api/

# Restore dependencies for the API project and all its dependencies
RUN dotnet restore src/RTSec.Kryptonian.Api/RTSec.Kryptonian.Api.csproj

# Copy source code
COPY src/ src/

# Build and publish
WORKDIR /src/src/RTSec.Kryptonian.Api
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install OpenSSL for certificate operations and curl for healthcheck
RUN apt-get update && apt-get install -y --no-install-recommends \
    openssl \
    ca-certificates \
    curl \
    && rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN groupadd -r kryptonian && useradd -r -g kryptonian kryptonian

# Create directories for logs and certificates
RUN mkdir -p /app/logs /app/certs && chown -R kryptonian:kryptonian /app

# Copy published application
COPY --from=build /app/publish .

# Set ownership
RUN chown -R kryptonian:kryptonian /app

# Switch to non-root user
USER kryptonian

# Environment variables
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
ENV Logging__Console__FormatterName=Json

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:5000/api/status/health || exit 1

# Expose ports
EXPOSE 5000

ENTRYPOINT ["dotnet", "RTSec.Kryptonian.Api.dll"]
