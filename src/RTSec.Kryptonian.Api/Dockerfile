# syntax=docker/dockerfile:1.7

# ── UI stage ──────────────────────────────────────────────────────────────────
# Builds the Vite admin dashboard. VITE_API_KEY is baked into the bundle so the
# dashboard sends X-API-Key against the deployed gateway.
# Pulls from AWS ECR Public's Docker Hub mirror to bypass Docker Hub's
# anonymous-pull rate limit that breaks ACR builds intermittently.
FROM public.ecr.aws/docker/library/node:22-alpine AS ui
WORKDIR /ui
ARG VITE_API_KEY=""
COPY src/RTSec.Kryptonian.Ui/package.json src/RTSec.Kryptonian.Ui/package-lock.json ./
RUN npm ci
COPY src/RTSec.Kryptonian.Ui/ ./
RUN echo "VITE_API_KEY=$VITE_API_KEY" > .env.production && npm run build

# ── .NET build stage ──────────────────────────────────────────────────────────
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

# ── Runtime stage ─────────────────────────────────────────────────────────────
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

# Create directories for logs, certificates, and the SQLite data volume
RUN mkdir -p /app/logs /app/certs /app/wwwroot/ui /data && chown -R kryptonian:kryptonian /app /data

# Copy published application
COPY --from=build /app/publish .

# Copy the built admin dashboard. Program.cs serves /app/wwwroot/ui in addition
# to the legacy dev path under ../RTSec.Kryptonian.Ui/dist.
COPY --from=ui /ui/dist/ /app/wwwroot/ui/

# Set ownership after copies so the kryptonian user owns the new files
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
