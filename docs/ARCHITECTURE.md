# RTSec.Kryptonian Architecture

This document describes the architecture and design of RTSec.Kryptonian, a certificate management gateway implementing the EST protocol (RFC 7030).

## Table of Contents

1. [Overview](#overview)
2. [System Context](#system-context)
3. [Project Structure](#project-structure)
4. [Layer Architecture](#layer-architecture)
5. [Key Components](#key-components)
6. [Data Flow](#data-flow)
7. [Security Architecture](#security-architecture)
8. [Database Design](#database-design)
9. [Extension Points](#extension-points)

---

## Overview

RTSec.Kryptonian is a unified certificate management gateway designed for medical device environments. It acts as a proxy between EST-enabled devices and various Certificate Authority (CA) backends, providing:

- **EST Protocol Implementation** (RFC 7030): `/cacerts`, `/simpleenroll`, `/simplereenroll`
- **Multi-CA Support**: Self-Signed, ACME (Let's Encrypt, ZeroSSL), with architecture for ADCS/EJBCA
- **Admin REST API**: Management of CA backends, EST profiles, and enrollment monitoring
- **Audit Trail**: Complete logging of all certificate operations

### Design Principles

- **Clean Architecture**: Clear separation between domain, application, infrastructure, and presentation layers
- **OpenAPI-First**: All APIs designed contract-first with OpenAPI specifications
- **Security by Default**: TLS 1.2+, secrets from environment/vault, rate limiting
- **Extensibility**: Factory pattern for CA connectors, pluggable challenge providers

---

## System Context

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              External Systems                                │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐                  │
│  │   Medical    │    │  Let's       │    │   ZeroSSL    │                  │
│  │   Devices    │    │  Encrypt     │    │   (ACME)     │                  │
│  │   (EST)      │    │  (ACME)      │    │              │                  │
│  └──────┬───────┘    └──────┬───────┘    └──────┬───────┘                  │
│         │                   │                   │                           │
│         │ EST Protocol      │ ACME Protocol     │                           │
│         │ (mTLS)            │ (HTTPS)           │                           │
│         ▼                   ▼                   ▼                           │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                     RTSec.Kryptonian                                │   │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐                  │   │
│  │  │  EST API    │  │  Admin API  │  │  Blazor UI  │                  │   │
│  │  │  (RFC 7030) │  │  (REST)     │  │  (Web)      │                  │   │
│  │  └─────────────┘  └─────────────┘  └─────────────┘                  │   │
│  │         │                │                │                          │   │
│  │         └────────────────┼────────────────┘                          │   │
│  │                          ▼                                           │   │
│  │              ┌─────────────────────┐                                 │   │
│  │              │    PostgreSQL DB    │                                 │   │
│  │              └─────────────────────┘                                 │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Project Structure

```
RTSec.Kryptonian/
├── src/
│   ├── RTSec.Kryptonian.Domain/           # Core business entities and interfaces
│   ├── RTSec.Kryptonian.Application/      # Use cases, services, DTOs
│   ├── RTSec.Kryptonian.Infrastructure/   # External concerns (DB, CA connectors)
│   ├── RTSec.Kryptonian.Api/              # ASP.NET Core Web API
│   ├── RTSec.Kryptonian.Web/              # Blazor Server Admin UI
│   └── RTSec.Kryptonian.Shared/           # Cross-cutting utilities
├── tests/
│   ├── RTSec.Kryptonian.Domain.Tests/
│   ├── RTSec.Kryptonian.Application.Tests/
│   ├── RTSec.Kryptonian.Infrastructure.Tests/
│   └── RTSec.Kryptonian.Api.Tests/
├── docs/                                    # OpenAPI specs, documentation
├── scripts/                                 # Utility scripts
└── docker-compose.yml                       # Container orchestration
```

---

## Layer Architecture

The solution follows Clean Architecture principles with four distinct layers:

```
┌─────────────────────────────────────────────────────────────────┐
│                      Presentation Layer                          │
│  ┌─────────────────────────┐  ┌─────────────────────────────┐   │
│  │   RTSec.Kryptonian.Api │  │   RTSec.Kryptonian.Web     │   │
│  │   - EST Controller      │  │   - Blazor Components       │   │
│  │   - Admin Controllers   │  │   - MudBlazor UI            │   │
│  │   - Middleware          │  │   - API Client Service      │   │
│  └────────────┬────────────┘  └──────────────┬──────────────┘   │
│               │                              │                   │
├───────────────┼──────────────────────────────┼───────────────────┤
│               ▼                              ▼                   │
│                      Application Layer                           │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │            RTSec.Kryptonian.Application                 │    │
│  │   - CaBackendService          - EnrollmentOrchestrator  │    │
│  │   - EstProfileService         - DTOs & Mapping          │    │
│  │   - EnrollmentEventService    - Validators              │    │
│  └────────────────────────────┬────────────────────────────┘    │
│                               │                                  │
├───────────────────────────────┼──────────────────────────────────┤
│                               ▼                                  │
│                       Domain Layer                               │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │              RTSec.Kryptonian.Domain                    │    │
│  │   - Entities (CaBackend, EstProfile, Certificate, etc.) │    │
│  │   - Value Objects (ParsedCsr, CertificateIssuanceResult) │    │
│  │   - Interfaces (ICaConnector, IUnitOfWork, etc.)        │    │
│  │   - Enums (CaBackendType, EnrollmentStatus, etc.)       │    │
│  └─────────────────────────────────────────────────────────┘    │
│                               ▲                                  │
├───────────────────────────────┼──────────────────────────────────┤
│                               │                                  │
│                    Infrastructure Layer                          │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │          RTSec.Kryptonian.Infrastructure                │    │
│  │   - EF Core DbContext       - SelfSignedCaConnector     │    │
│  │   - Repositories            - AcmeCaConnector           │    │
│  │   - PkcsService             - CaConnectorFactory        │    │
│  │   - DataProtectionService   - Challenge Providers       │    │
│  └─────────────────────────────────────────────────────────┘    │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Layer Responsibilities

| Layer | Responsibility | Dependencies |
|-------|---------------|--------------|
| **Domain** | Business entities, interfaces, value objects | None (innermost) |
| **Application** | Use cases, orchestration, DTOs, validation | Domain |
| **Infrastructure** | Database, external APIs, cryptography | Domain, Application |
| **Presentation** | HTTP endpoints, UI, serialization | Application |

---

## Key Components

### Domain Layer

#### Entities

| Entity | Purpose |
|--------|---------|
| `CaBackend` | Represents a Certificate Authority backend (Self-Signed, ACME, etc.) |
| `EstProfile` | Configuration for EST endpoint routing and certificate policies |
| `Certificate` | Issued certificate record with metadata |
| `EnrollmentEvent` | Audit record for enrollment operations |
| `AcmeAccount` | ACME account credentials (encrypted) |

#### Key Interfaces

```csharp
// CA Connector - Abstracts different CA implementations
interface ICaConnector
{
    CaBackendType Type { get; }
    Task<X509Certificate2[]> GetCaCertificatesAsync(CancellationToken ct);
    Task<CertificateIssuanceResult> IssueCertificateAsync(ParsedCsr csr, EstProfile profile, CancellationToken ct);
    Task<bool> RevokeCertificateAsync(string serial, RevocationReason reason, CancellationToken ct);
    Task<bool> TestConnectionAsync(CancellationToken ct);
}

// PKCS Service - EST protocol encoding/decoding
interface IPkcsService
{
    ParsedCsr ParsePkcs10(byte[] csrBytes);
    Task<byte[]> DecodeEstRequestBodyAsync(Stream body, string? contentTransferEncoding, int maxSize, CancellationToken ct);
    byte[] EncodeToPkcs7(X509Certificate2[] certs);
    byte[] EncodeEstResponseBody(byte[] pkcs7);
}

// Enrollment Orchestrator - Coordinates enrollment flow
interface IEnrollmentOrchestrator
{
    Task<byte[]> GetCaCertsAsync(Guid profileId, CancellationToken ct);
    Task<EnrollmentResult> EnrollAsync(Guid profileId, byte[] csrBytes, string? deviceId, string? clientIp, CancellationToken ct);
    Task<EnrollmentResult> ReenrollAsync(Guid profileId, byte[] csrBytes, X509Certificate2 existingCert, string? clientIp, CancellationToken ct);
}
```

### Infrastructure Layer

#### CA Connectors

```
ICaConnector (Interface)
    │
    ├── SelfSignedCaConnector
    │   └── Signs CSRs using a local CA certificate
    │       Uses: BouncyCastle for X.509 certificate generation
    │
    └── AcmeCaConnector
        └── Obtains certificates from ACME CAs (Let's Encrypt, ZeroSSL)
            Uses: Certes library for ACME protocol
            Supports: HTTP-01 and DNS-01 challenges
```

#### CA Connector Factory

```csharp
// Creates appropriate connector based on backend type
class CaConnectorFactory : ICaConnectorFactory
{
    ICaConnector CreateConnector(CaBackend backend)
    {
        return backend.Type switch
        {
            CaBackendType.SelfSigned => CreateSelfSignedConnector(backend),
            CaBackendType.Acme => CreateAcmeConnector(backend),
            _ => throw new NotSupportedException($"Backend type {backend.Type} not supported")
        };
    }
}
```

### Application Layer

#### Services

| Service | Purpose |
|---------|---------|
| `CaBackendService` | CRUD operations for CA backends |
| `EstProfileService` | CRUD operations for EST profiles |
| `EnrollmentEventService` | Query enrollment audit logs |
| `EnrollmentOrchestrator` | Coordinates the enrollment workflow |

#### Enrollment Flow

```
┌──────────────┐     ┌────────────────────┐     ┌─────────────────┐
│ EST Request  │────▶│ EnrollmentOrchest- │────▶│  CaConnector    │
│ (CSR)        │     │ rator              │     │  Factory        │
└──────────────┘     └────────────────────┘     └────────┬────────┘
                              │                          │
                              │                          ▼
                              │                 ┌─────────────────┐
                              │                 │  ICaConnector   │
                              │                 │  (Self-Signed/  │
                              │                 │   ACME)         │
                              │                 └────────┬────────┘
                              │                          │
                              ▼                          ▼
                     ┌─────────────────┐        ┌─────────────────┐
                     │ EnrollmentEvent │        │  Certificate    │
                     │ (Audit Log)     │        │  (Issued)       │
                     └─────────────────┘        └─────────────────┘
```

---

## Data Flow

### EST Simple Enrollment Flow

```
┌─────────┐                    ┌─────────────┐                    ┌──────────────┐
│ Device  │                    │ Kryptonian  │                    │ CA Backend   │
│         │                    │ API         │                    │              │
└────┬────┘                    └──────┬──────┘                    └──────┬───────┘
     │                                │                                  │
     │ 1. POST /simpleenroll          │                                  │
     │    (PKCS#10 CSR, base64)       │                                  │
     │───────────────────────────────▶│                                  │
     │                                │                                  │
     │                                │ 2. Decode CSR                    │
     │                                │    Validate signature            │
     │                                │    Look up EST profile           │
     │                                │                                  │
     │                                │ 3. IssueCertificateAsync()       │
     │                                │─────────────────────────────────▶│
     │                                │                                  │
     │                                │         4. Sign certificate      │
     │                                │◀─────────────────────────────────│
     │                                │                                  │
     │                                │ 5. Create EnrollmentEvent        │
     │                                │    Store Certificate             │
     │                                │                                  │
     │ 6. 200 OK                      │                                  │
     │    (PKCS#7, base64)            │                                  │
     │◀───────────────────────────────│                                  │
     │                                │                                  │
```

### ACME Certificate Issuance Flow

```
┌─────────────┐     ┌───────────────┐     ┌─────────────┐     ┌────────────┐
│ Kryptonian  │     │ AcmeCa-       │     │ ACME CA     │     │ Challenge  │
│ API         │     │ Connector     │     │ (Let's Enc) │     │ Provider   │
└──────┬──────┘     └───────┬───────┘     └──────┬──────┘     └─────┬──────┘
       │                    │                    │                  │
       │ IssueCertificate   │                    │                  │
       │───────────────────▶│                    │                  │
       │                    │                    │                  │
       │                    │ 1. Create Order    │                  │
       │                    │───────────────────▶│                  │
       │                    │                    │                  │
       │                    │ 2. Get Challenges  │                  │
       │                    │◀───────────────────│                  │
       │                    │                    │                  │
       │                    │ 3. Prepare HTTP-01 │                  │
       │                    │────────────────────┼─────────────────▶│
       │                    │                    │                  │
       │                    │ 4. Validate        │                  │
       │                    │───────────────────▶│                  │
       │                    │                    │                  │
       │                    │                    │ 5. Fetch token   │
       │                    │                    │─────────────────▶│
       │                    │                    │◀─────────────────│
       │                    │                    │                  │
       │                    │ 6. Finalize Order  │                  │
       │                    │───────────────────▶│                  │
       │                    │                    │                  │
       │                    │ 7. Download Cert   │                  │
       │                    │◀───────────────────│                  │
       │                    │                    │                  │
       │ 8. Return Cert     │                    │                  │
       │◀───────────────────│                    │                  │
       │                    │                    │                  │
```

---

## Security Architecture

### Authentication Model

| Endpoint | Authentication | Description |
|----------|---------------|-------------|
| `/api/*` (Admin) | API Key (`X-API-Key` header) | Admin operations |
| `/.well-known/est/*` | mTLS (Client Certificate) | Device enrollment |
| `/.well-known/acme-challenge/*` | None (public) | ACME HTTP-01 validation |

### Network Security

```
┌─────────────────────────────────────────────────────────────────┐
│                        External Network                          │
│                                                                  │
│  ┌──────────────────┐                                           │
│  │  Medical Devices │                                           │
│  └────────┬─────────┘                                           │
│           │ TLS 1.2+ with mTLS                                  │
│           ▼                                                      │
├──────────────────────────────────────────────────────────────────┤
│                         DMZ / Edge                               │
│                                                                  │
│  ┌──────────────────────────────────────────────────────┐       │
│  │              Reverse Proxy (nginx/Traefik)            │       │
│  │              - TLS termination                        │       │
│  │              - Rate limiting                          │       │
│  │              - WAF rules                              │       │
│  └────────────────────────┬─────────────────────────────┘       │
│                           │                                      │
├───────────────────────────┼──────────────────────────────────────┤
│                           ▼                                      │
│                    Internal Network                              │
│                                                                  │
│  ┌──────────────────┐  ┌──────────────────┐                     │
│  │ Kryptonian API   │  │ Kryptonian Web   │                     │
│  │ (EST + Admin)    │  │ (Blazor UI)      │                     │
│  └────────┬─────────┘  └────────┬─────────┘                     │
│           │                     │                                │
│           └──────────┬──────────┘                                │
│                      ▼                                           │
│           ┌──────────────────┐                                   │
│           │   PostgreSQL     │                                   │
│           │   (encrypted)    │                                   │
│           └──────────────────┘                                   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Secrets Management

| Secret | Storage | Usage |
|--------|---------|-------|
| Database password | Environment variable | PostgreSQL connection |
| Admin API keys | Environment variable | `X-API-Key` validation |
| CA private key | File (PFX/PEM) or DB (encrypted) | Self-signed certificate signing |
| ACME account key | Database (encrypted) | ACME protocol authentication |
| Server TLS cert | File (PFX) | Kestrel HTTPS |

### Data Protection

- **ACME Account Keys**: Encrypted using ASP.NET Core Data Protection API
- **PFX Passwords**: Never stored, passed via environment variables
- **Audit Logs**: All enrollment operations logged with timestamps, IPs, device IDs

---

## Database Design

### Entity Relationship Diagram

```
┌─────────────────┐       ┌─────────────────┐       ┌─────────────────┐
│   CaBackend     │       │   EstProfile    │       │  Certificate    │
├─────────────────┤       ├─────────────────┤       ├─────────────────┤
│ Id (PK)         │◀──┐   │ Id (PK)         │◀──┐   │ Id (PK)         │
│ Name            │   │   │ Name            │   │   │ SerialNumber    │
│ Type            │   │   │ PathPrefix      │   │   │ SubjectDn       │
│ Url             │   └───│ CaBackendId(FK) │   │   │ IssuerDn        │
│ Config (JSONB)  │       │ Hostnames[]     │   │   │ Thumbprint      │
│ IsEnabled       │       │ ValidityDays    │   └───│ EstProfileId(FK)│
│ CreatedAt       │       │ RequireClientCrt│       │ NotBefore       │
│ UpdatedAt       │       │ IsEnabled       │       │ NotAfter        │
└─────────────────┘       │ CreatedAt       │       │ Status          │
                          │ UpdatedAt       │       │ CertificatePem  │
                          └─────────────────┘       │ DeviceId        │
                                  │                 │ CreatedAt       │
                                  │                 └─────────────────┘
                                  │
                                  ▼
                          ┌─────────────────┐
                          │ EnrollmentEvent │
                          ├─────────────────┤
                          │ Id (PK)         │
                          │ Timestamp       │
                          │ ProfileId (FK)  │
                          │ DeviceId        │
                          │ Status          │
                          │ SubjectDn       │
                          │ RequestorIp     │
                          │ ErrorMessage    │
                          │ CertificateId   │
                          └─────────────────┘

┌─────────────────┐
│  AcmeAccount    │
├─────────────────┤
│ Id (PK)         │
│ DirectoryUrl    │
│ Email           │
│ AccountUrl      │
│ EncryptedKey    │
│ EabKeyId        │
│ EncryptedEabKey │
│ IsActive        │
│ CreatedAt       │
└─────────────────┘
```

### Key Indexes

| Table | Index | Purpose |
|-------|-------|---------|
| `EstProfile` | `UNIQUE(PathPrefix, Hostname)` | EST routing lookup |
| `Certificate` | `INDEX(SerialNumber)` | Certificate lookup |
| `Certificate` | `INDEX(Thumbprint)` | Certificate validation |
| `EnrollmentEvent` | `INDEX(ProfileId, Timestamp)` | Audit queries |
| `AcmeAccount` | `UNIQUE(DirectoryUrl, Email)` | Account lookup |

---

## Extension Points

### Adding a New CA Backend

1. **Define the backend type** in `CaBackendType` enum
2. **Implement `ICaConnector`** interface
3. **Register in `CaConnectorFactory`**
4. **Add configuration parsing** in factory method
5. **Update UI** with new backend options

```csharp
// Example: Adding EJBCA support
public class EjbcaCaConnector : ICaConnector
{
    public CaBackendType Type => CaBackendType.Ejbca;

    public async Task<CertificateIssuanceResult> IssueCertificateAsync(
        ParsedCsr csr, EstProfile profile, CancellationToken ct)
    {
        // Implement EJBCA REST API integration
    }
}
```

### Adding a New ACME Challenge Type

1. **Implement `IAcmeChallengeProvider`** interface
2. **Register in DI container**
3. **Update `AcmeCaConnector`** to use new provider

```csharp
public interface IAcmeChallengeProvider
{
    string ChallengeType { get; }  // "http-01", "dns-01", etc.
    Task PrepareAsync(string domain, string token, string keyAuth, CancellationToken ct);
    Task CleanupAsync(string domain, string token, CancellationToken ct);
}
```

### Adding EST Extensions

The EST controller can be extended with additional endpoints per RFC 7030:

- `/serverkeygen` - Server-side key generation
- `/csrattrs` - CSR attribute requirements
- `/fullcmc` - Full CMC enrollment

---

## Technology Stack

| Component | Technology | Purpose |
|-----------|------------|---------|
| Runtime | .NET 8 | Application framework |
| Web Framework | ASP.NET Core | HTTP endpoints |
| UI Framework | Blazor Server | Admin interface |
| UI Components | MudBlazor | Material Design components |
| Database | PostgreSQL 16 | Persistent storage |
| ORM | Entity Framework Core 8 | Data access |
| Cryptography | BouncyCastle | PKCS#7, X.509 generation |
| ACME Client | Certes | ACME protocol |
| Logging | Serilog | Structured logging |
| Containerization | Docker | Deployment |
| CI/CD | GitHub Actions | Build and test automation |

---

## Performance Considerations

### Rate Limiting

EST endpoints are rate-limited using `AspNetCoreRateLimit`:

```json
{
  "IpRateLimiting": {
    "EnableEndpointRateLimiting": true,
    "StackBlockedRequests": false,
    "RealIpHeader": "X-Forwarded-For",
    "GeneralRules": [
      {
        "Endpoint": "*:/.well-known/est/*",
        "Period": "1m",
        "Limit": 100
      }
    ]
  }
}
```

### Connection Pooling

PostgreSQL connections are pooled via Npgsql:

```
Host=localhost;Database=kryptonian;Pooling=true;MinPoolSize=5;MaxPoolSize=100
```

### Certificate Caching

- CA certificates are cached in memory
- ACME CA chain cached after first issuance
- EST profile lookups cached by path/hostname

---

## Observability

### Logging

Structured logging with Serilog:

```
[14:32:15 INF] [abc123] Certificate issued: Serial=01AB, Subject=CN=device-001
[14:32:16 WRN] [def456] ACME challenge failed: Domain=test.example.com, Error=DNS timeout
```

Enrichers:
- `CorrelationId` - Request tracking
- `ClientIp` - Client identification
- `RequestId` - Unique request ID

### Health Checks

```
GET /api/status/health

{
  "status": "Healthy",
  "components": {
    "database": "Healthy",
    "ca_connectors": "Healthy"
  }
}
```

### Metrics (Future)

Prometheus metrics endpoint planned for:
- Enrollment counts by profile/status
- CA backend latency
- Request rates and errors
