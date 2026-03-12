# RTSec.Kryptonian

A unified certificate-management gateway implementing the EST protocol (RFC 7030) for medical device certificate enrollment. Built with .NET 8 and ASP.NET Core.

## Features

- **EST Protocol (RFC 7030)**: Full implementation of `/cacerts`, `/simpleenroll`, and `/simplereenroll` endpoints
- **Multiple CA Backends**: Self-Signed CA, ACME (Let's Encrypt, ZeroSSL), with extensible architecture for ADCS/EJBCA
- **Admin REST API**: Manage CA backends, EST profiles, and view enrollment events
- **Blazor Admin UI**: Web-based management interface with MudBlazor components
- **Audit Trail**: All enrollment events logged with device ID, client IP, and status
- **Docker Ready**: Multi-stage Docker builds with Docker Compose orchestration

## How It Works

Kryptonian acts as a middleman between medical devices and Certificate Authorities (CAs). Devices never talk directly to the CA - they only talk to Kryptonian, which handles all the complexity behind the scenes.

```
┌─────────────────┐         ┌─────────────────┐         ┌─────────────────┐
│  Medical Device │         │   Kryptonian    │         │  CA Backend     │
│  (CT Scanner,   │  ────►  │   (This App)    │  ────►  │  (Self-Signed,  │
│   MRI, etc.)    │         │                 │         │   ACME, ADCS)   │
└─────────────────┘         └─────────────────┘         └─────────────────┘
     Only talks to              The Hub                   Device never
     Kryptonian                                           sees this
```

### The Enrollment Flow (Plain English)

When a medical device needs a certificate, here's what happens:

| Step | Device Asks | Kryptonian Does | Component |
|------|-------------|-----------------|-----------|
| **1. Get CA Info** | "Who will be signing my certificate?" | Returns the CA certificate so the device knows who to trust | `EstController` → `EnrollmentOrchestrator` |
| **2. Request Certificate** | "Here's my identity info (CSR). Can I get a certificate?" | Validates the request, asks the CA backend to sign it, returns the signed certificate | `EstController` → `CaConnectorFactory` → `SelfSignedCaConnector` or `AcmeCaConnector` |
| **3. Renew Certificate** | "My certificate is expiring. Here's my current cert and a new request." | Verifies the device's existing certificate, issues a fresh one | `EstController` → `EnrollmentOrchestrator` (with mTLS validation) |

### Why This Matters

**Without Kryptonian:** Every device vendor implements their own certificate logic. Hospitals manage dozens of different CA integrations. Certificates expire unexpectedly. It's a mess.

**With Kryptonian:** Devices implement one simple protocol (EST). Hospitals configure one gateway. Kryptonian handles the rest - whether the backend is a simple self-signed CA, Let's Encrypt, Microsoft ADCS, or anything else.

### Key Components

| Component | What It Does |
|-----------|--------------|
| **EST Controller** (`RTSec.Kryptonian.Api`) | Receives device requests at `/.well-known/est/*` endpoints |
| **Enrollment Orchestrator** (`RTSec.Kryptonian.Application`) | Coordinates the enrollment flow, applies policies, logs events |
| **CA Connectors** (`RTSec.Kryptonian.Infrastructure`) | Talks to actual CAs (self-signed, ACME, etc.) |
| **Admin API** (`RTSec.Kryptonian.Api`) | Lets administrators configure CA backends and EST profiles |
| **Database** (PostgreSQL) | Stores configuration, certificates, and audit logs |

## Quick Start

Use the helper scripts for the easiest local startup/shutdown flow:

```bash
# Start backend + frontend (web profile)
./start-up.sh

# Stop services and clean up containers/networks
./tear-down.sh

# Optional: also remove DB volume/data
./tear-down.sh --volumes
```

Manual startup (equivalent low-level commands):

```bash
# Generate test certificates
chmod +x scripts/generate-test-certs.sh
./scripts/generate-test-certs.sh

# Start with Docker Compose
docker compose up -d

# Verify health
curl http://localhost:5000/api/status/health
```

## Development Container (VS Code)

This repository includes a VS Code dev container configuration under `.devcontainer/`.

### Included Tools

- .NET 8 SDK
- OpenSSL
- PostgreSQL client tools (`psql`)
- Docker CLI access from inside the container (via dev container feature)

### Open In Dev Container

1. Install Docker Desktop and the VS Code Dev Containers extension.
2. Open this repository in VS Code.
3. Run: `Dev Containers: Reopen in Container`.

The dev container starts a dedicated `devcontainer` service and the existing `postgres` service from Compose.

### First Run Behavior

- `dotnet restore RTSec.Kryptonian.sln` runs automatically.
- `dotnet-ef` is installed or updated as a global tool.
- Ports `5000`, `5001`, and `5432` are forwarded.

### Typical Commands Inside Container

```bash
dotnet build RTSec.Kryptonian.sln
dotnet test RTSec.Kryptonian.sln
docker compose up -d
```

## Blazor Admin Portal

The project includes a web-based admin interface built with Blazor Server and MudBlazor components. The admin portal is **optional** and runs as a separate Docker profile.

### Starting the Admin Portal

```bash
# Start all services INCLUDING the admin portal
docker compose --profile web up -d
```

### Services Overview

| Service | Container | Port | URL |
|---------|-----------|------|-----|
| PostgreSQL | `kryptonian-db` | 5432 | - |
| API Server | `kryptonian-api` | 5000 | http://localhost:5000 |
| Admin Portal | `kryptonian-web` | 5001 | http://localhost:5001 |

### Accessing the Admin Portal

Once running, open your browser to: **http://localhost:5001**

The admin portal provides:
- **CA Backends** - Configure certificate authorities (Self-Signed, ACME, etc.)
- **EST Profiles** - Manage enrollment profiles for devices
- **Events** - View enrollment audit logs and history
- **Dashboard** - System status overview

### Admin Portal Commands

```bash
# Start with admin portal
docker compose --profile web up -d

# View logs
docker compose --profile web logs -f kryptonian-web

# Rebuild after code changes
docker compose --profile web up -d --build

# Stop all services
docker compose --profile web down
```

### Running Without the Admin Portal

If you only need the API (headless mode):
```bash
docker compose up -d
```

This starts only the database and API server, without the web UI.

## Demo - Device Enrollment

We provide an interactive demo script that walks through the complete enrollment flow. The script pauses at each step so you can explain what's happening.

### Prerequisites

- **Docker Desktop** (Windows/Mac) or **Docker Engine** (Linux) - [Install Docker](https://docs.docker.com/get-docker/)
- **OpenSSL** - Pre-installed on Mac/Linux. For Windows: `winget install OpenSSL.Light`

### First-Time Setup

**Step 1: Generate Test Certificates**

The demo needs test CA certificates. Run this once from the `scripts/` folder:

Windows (PowerShell):
```powershell
cd scripts
.\generate-test-certs.ps1
```

Linux/Mac:
```bash
cd scripts
chmod +x generate-test-certs.sh
./generate-test-certs.sh
```

This creates a `certs/` folder with test CA, server, and client certificates.

**Step 2: Build and Start Services**

From the **project root directory** (not scripts/):

```bash
cd ..
docker compose build
docker compose up -d
```

Wait about 15 seconds for the database to initialize. Check status with:
```bash
docker compose ps
```

Both `kryptonian-api` and `kryptonian-db` should show as "running" or "healthy".

**Step 3: Run the Demo**

Once the services are running, run the demo from the `scripts/` folder:

Windows (PowerShell):
```powershell
cd scripts
.\demo-enrollment.ps1
```

Linux/Mac:
```bash
cd scripts
./demo-enrollment.sh
```

The demo will walk you through each step, pausing for you to read what's happening.

**Stopping the Services**

When finished, stop the services from the project root:
```bash
cd ..
docker compose down
```

### What the Demo Shows

| Step | Who | What Happens |
|------|-----|--------------|
| 1 | Admin | Configures a CA backend (tells Kryptonian which CA to use) |
| 2 | Admin | Creates an EST profile (sets up the enrollment endpoint) |
| 3 | Device | Asks "Who will sign my certificate?" and gets the CA cert |
| 4 | Device | Generates a private key and certificate request (CSR) |
| 5 | Device | Sends request to hub, receives signed certificate |
| 6 | Device | Installs the certificate for secure communication |
| 7 | Admin | Views the enrollment in the audit log |

### Demo Options

```powershell
# Start services automatically and use a custom device name
.\scripts\demo-enrollment.ps1 -StartServices -DeviceName "MRI-Scanner-002"
```

```bash
# Same options for bash
./scripts/demo-enrollment.sh --start-services --device-name "MRI-Scanner-002"
```

### Demo Output

The script creates files in `demo-output/`:
- `device.key` - Device's private key (secret)
- `device_cert.pem` - Issued certificate (public)
- `device.pfx` - Combined bundle for the device

## Project Structure

```
RTSec.Kryptonian/
├── src/
│   ├── RTSec.Kryptonian.Domain/           # Entities, interfaces, value objects
│   ├── RTSec.Kryptonian.Application/      # Services, DTOs, validators
│   ├── RTSec.Kryptonian.Infrastructure/   # EF Core, CA connectors, cryptography
│   ├── RTSec.Kryptonian.Api/              # ASP.NET Core Web API (EST + Admin)
│   ├── RTSec.Kryptonian.Web/              # Blazor Server admin UI
│   └── RTSec.Kryptonian.Shared/           # Constants, extensions
├── tests/
│   ├── RTSec.Kryptonian.Domain.Tests/
│   ├── RTSec.Kryptonian.Application.Tests/
│   ├── RTSec.Kryptonian.Infrastructure.Tests/
│   └── RTSec.Kryptonian.Api.Tests/
├── docs/                                    # OpenAPI specs and documentation
├── scripts/                                 # Utility scripts
├── docker-compose.yml                       # Container orchestration
└── RTSec.Kryptonian.sln
```

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string | - |
| `ADMIN_API_KEYS` | Comma-separated API keys | `dev-api-key-change-in-production` |
| `CA_PFX_PASSWORD` | Password for CA PFX file | `TestPassword123!` |
| `ASPNETCORE_ENVIRONMENT` | Environment name | `Development` |

### CA Backend Support

Kryptonian is designed to support multiple Certificate Authority backends. The hub abstracts the CA implementation, so devices use the same EST protocol regardless of which CA is behind it.

| CA Backend | Status | Description | Use Case |
|------------|--------|-------------|----------|
| **Self-Signed** | Done | Local CA certificate for signing | Development, testing, isolated networks |
| **ACME** | Done | Let's Encrypt, ZeroSSL, other ACME CAs | Public certificates, automated issuance |
| **Microsoft ADCS** | Planned | Active Directory Certificate Services | Enterprise Windows environments, hospitals |
| **EJBCA** | Planned | Enterprise Java Beans CA | Large organizations, open-source enterprise PKI |
| **CFSSL** | Planned | CloudFlare's PKI toolkit | Kubernetes, microservices |
| **HashiCorp Vault** | Planned | PKI secrets engine | DevOps, cloud-native, secrets management |
| **Smallstep** | Planned | Modern open-source CA | Internal PKI, zero-trust environments |
| **OpenXPKI** | Planned | Open-source enterprise PKI | Government, healthcare, high compliance |

To implement a new CA backend, create a class that implements `ICaConnector` and register it in `CaConnectorFactory`.

## API Endpoints

### Admin API (`/api/*`)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/status/health` | Health check |
| GET/POST | `/api/cas` | List/create CA backends |
| GET/PUT/DELETE | `/api/cas/{id}` | Manage CA backend |
| GET/POST | `/api/est-profiles` | List/create EST profiles |
| GET/PUT/DELETE | `/api/est-profiles/{id}` | Manage EST profile |
| GET | `/api/status/enrollments` | List enrollment events |

### EST Protocol (`/.well-known/est/*`)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/.well-known/est/{profile}/cacerts` | Get CA certificates |
| POST | `/.well-known/est/{profile}/simpleenroll` | Simple enrollment |
| POST | `/.well-known/est/{profile}/simplereenroll` | Certificate renewal |

## Development

### Prerequisites

- .NET 8.0 SDK
- PostgreSQL 16+ (or Docker)
- OpenSSL

### Build and Test

```bash
# Restore and build
dotnet restore
dotnet build

# Run tests
dotnet test

# Run locally
dotnet run --project src/RTSec.Kryptonian.Api
```

### Docker Build

```bash
# Build images
docker compose build

# Run with PostgreSQL
docker compose up -d

# Include admin UI
docker compose --profile web up -d
```

## Documentation

- **[API Guide](docs/API-GUIDE.md)** - Complete API usage guide with C# examples and enrollment flow
- **[Architecture](docs/ARCHITECTURE.md)** - System design, components, and data flow diagrams
- **[Deployment Guide](docs/DEPLOYMENT.md)** - Production deployment and security configuration

### OpenAPI Specifications

The API is documented with OpenAPI specifications in the repository root:

| File | Description |
|------|-------------|
| [OpenAPI.Admin.yaml](OpenAPI.Admin.yaml) | CA Backend management (`/api/cas`) |
| [OpenAPI.ESTProfiles.yaml](OpenAPI.ESTProfiles.yaml) | EST Profile management (`/api/est-profiles`) |
| [OpenAPI.StatusObserve.yaml](OpenAPI.StatusObserve.yaml) | Health and enrollment events (`/api/status/*`) |
| [OpenAPI.Schema.yaml](OpenAPI.Schema.yaml) | Shared data type definitions |

Swagger UI is available at `http://localhost:5000/swagger` when running in Development mode.

## Architecture

Built with Clean Architecture principles:

- **Domain**: Core business entities and interfaces
- **Application**: Use cases, DTOs, and service implementations
- **Infrastructure**: Database access, CA connectors, cryptography
- **API**: REST controllers and EST protocol handlers
- **Web**: Blazor Server admin interface

## Security

- TLS 1.2+ required for EST endpoints
- API key authentication for admin endpoints
- mTLS support for client certificate authentication
- Non-root container execution
- Rate limiting on EST endpoints

## License

See LICENSE file
