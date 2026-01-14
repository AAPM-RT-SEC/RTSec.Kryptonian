# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for restore (all src projects)
COPY src/RTSec.Kryptonian.Shared/RTSec.Kryptonian.Shared.csproj src/RTSec.Kryptonian.Shared/
COPY src/RTSec.Kryptonian.Domain/RTSec.Kryptonian.Domain.csproj src/RTSec.Kryptonian.Domain/
COPY src/RTSec.Kryptonian.Application/RTSec.Kryptonian.Application.csproj src/RTSec.Kryptonian.Application/
COPY src/RTSec.Kryptonian.Web/RTSec.Kryptonian.Web.csproj src/RTSec.Kryptonian.Web/

# Restore dependencies for the Web project and all its dependencies
RUN dotnet restore src/RTSec.Kryptonian.Web/RTSec.Kryptonian.Web.csproj

# Copy source code
COPY src/ src/

# Build and publish
WORKDIR /src/src/RTSec.Kryptonian.Web
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create non-root user for security
RUN groupadd -r kryptonian && useradd -r -g kryptonian kryptonian

# Create directories
RUN mkdir -p /app/logs && chown -R kryptonian:kryptonian /app

# Copy published application
COPY --from=build /app/publish .

# Set ownership
RUN chown -R kryptonian:kryptonian /app

# Switch to non-root user
USER kryptonian

# Environment variables
ENV ASPNETCORE_URLS=http://+:5001
ENV ASPNETCORE_ENVIRONMENT=Production

# Expose ports
EXPOSE 5001

ENTRYPOINT ["dotnet", "RTSec.Kryptonian.Web.dll"]
