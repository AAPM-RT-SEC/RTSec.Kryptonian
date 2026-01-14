# RTSec.Kryptonian API Guide

This guide explains how to use the Kryptonian certificate management API, including the EST protocol for device enrollment and the Admin API for configuration management.

## Table of Contents

1. [Overview](#overview)
2. [Authentication](#authentication)
3. [Certificate Enrollment Flow](#certificate-enrollment-flow)
4. [EST Protocol Endpoints](#est-protocol-endpoints)
5. [Admin API Endpoints](#admin-api-endpoints)
6. [C# Client Examples](#c-client-examples)
7. [OpenAPI Specifications](#openapi-specifications)
8. [Error Handling](#error-handling)

---

## Overview

Kryptonian provides two distinct APIs:

| API | Purpose | Authentication | Base Path |
|-----|---------|----------------|-----------|
| **EST Protocol** | Device certificate enrollment (RFC 7030) | mTLS (client certificate) | `/.well-known/est/` |
| **Admin API** | Configuration and monitoring | API Key (`X-API-Key` header) | `/api/` |

### Typical Deployment

```
┌─────────────────┐         ┌─────────────────┐         ┌─────────────────┐
│  Medical Device │  EST    │   Kryptonian    │  Signs  │   CA Backend    │
│  (Client)       │────────▶│   Gateway       │────────▶│  (Self-Signed/  │
│                 │  mTLS   │                 │         │   ACME)         │
└─────────────────┘         └─────────────────┘         └─────────────────┘
                                    ▲
                                    │ Admin API
                                    │ (API Key)
                            ┌───────┴───────┐
                            │  Admin User   │
                            │  / Blazor UI  │
                            └───────────────┘
```

---

## Authentication

### EST Protocol (Device Enrollment)

EST endpoints use **mutual TLS (mTLS)** for authentication:

- **Initial Enrollment (`/simpleenroll`)**: May allow unauthenticated requests depending on profile configuration
- **Re-enrollment (`/simplereenroll`)**: Requires the device to present its existing certificate

### Admin API

All Admin API endpoints require an API key in the `X-API-Key` header:

```http
GET /api/cas HTTP/1.1
Host: localhost:5000
X-API-Key: your-api-key-here
```

---

## Certificate Enrollment Flow

### Step-by-Step Process

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        Certificate Enrollment Flow                           │
└─────────────────────────────────────────────────────────────────────────────┘

1. SETUP (Administrator - One Time)
   ┌─────────────┐
   │ Admin       │
   │ configures: │
   │ - CA Backend│ ──▶ POST /api/cas
   │ - EST Profile│ ──▶ POST /api/est-profiles
   └─────────────┘

2. GET CA CERTIFICATES (Device - Optional)
   ┌─────────────┐                              ┌─────────────┐
   │   Device    │  GET /.well-known/est/cacerts │ Kryptonian  │
   │             │ ─────────────────────────────▶│             │
   │             │                               │             │
   │             │ ◀───────────────────────────── │             │
   │             │  PKCS#7 (CA cert chain)       │             │
   └─────────────┘                              └─────────────┘

3. GENERATE CSR (Device - Local)
   ┌─────────────┐
   │   Device    │
   │ generates:  │
   │ - Key pair  │
   │ - PKCS#10   │
   │   CSR       │
   └─────────────┘

4. ENROLL (Device)
   ┌─────────────┐                              ┌─────────────┐
   │   Device    │ POST /.well-known/est/       │ Kryptonian  │
   │             │      simpleenroll            │             │
   │             │ ─────────────────────────────▶│             │
   │             │  (PKCS#10 CSR, base64)       │             │
   │             │                               │             │
   │             │ ◀───────────────────────────── │             │
   │             │  PKCS#7 (signed cert chain)  │             │
   └─────────────┘                              └─────────────┘

5. INSTALL CERTIFICATE (Device - Local)
   ┌─────────────┐
   │   Device    │
   │ installs:   │
   │ - Certificate│
   │ - Private key│
   │ - CA chain  │
   └─────────────┘

6. RENEW (Device - When Certificate Expires)
   ┌─────────────┐                              ┌─────────────┐
   │   Device    │ POST /.well-known/est/       │ Kryptonian  │
   │ (with cert) │      simplereenroll          │             │
   │             │ ─────────────────────────────▶│             │
   │             │  (mTLS + new CSR)            │             │
   │             │                               │             │
   │             │ ◀───────────────────────────── │             │
   │             │  PKCS#7 (renewed cert)       │             │
   └─────────────┘                              └─────────────┘
```

---

## EST Protocol Endpoints

The EST (Enrollment over Secure Transport) protocol is defined in [RFC 7030](https://tools.ietf.org/html/rfc7030).

### Base URL

```
https://{host}/.well-known/est/{label}/
```

- `{label}` is optional - corresponds to an EST profile name
- Without label: `/.well-known/est/cacerts`
- With label: `/.well-known/est/myprofile/cacerts`

### GET /cacerts - Get CA Certificates

Retrieves the CA certificate chain that will sign issued certificates.

**Request:**
```http
GET /.well-known/est/cacerts HTTP/1.1
Host: localhost:5000
```

**Response:**
```http
HTTP/1.1 200 OK
Content-Type: application/pkcs7-mime; smime-type=certs-only
Content-Transfer-Encoding: base64

MIIBkzCB/QIJALHAwdLpYbPdMA0GCSqGSIb3DQEBCwUAMBExDzANBgNV...
```

**Response Body:** Base64-encoded PKCS#7 (degenerate SignedData containing certificates only)

### POST /simpleenroll - Simple Enrollment

Requests a new certificate by submitting a PKCS#10 Certificate Signing Request (CSR).

**Request:**
```http
POST /.well-known/est/simpleenroll HTTP/1.1
Host: localhost:5000
Content-Type: application/pkcs10
Content-Transfer-Encoding: base64

MIICVDCCATwCAQAwDzENMAsGA1UEAwwEdGVzdDCCASIwDQYJKoZIhvcNAQEB...
```

**Response (Success):**
```http
HTTP/1.1 200 OK
Content-Type: application/pkcs7-mime; smime-type=certs-only
Content-Transfer-Encoding: base64

MIIDXTCCAkWgAwIBAgIJAJC1HiIAZAiUMA0GCSqGSIb3Dq...
```

**Response (Pending - Async Issuance):**
```http
HTTP/1.1 202 Accepted
Retry-After: 60

{"message": "Certificate issuance pending", "retryAfter": 60}
```

### POST /simplereenroll - Simple Re-enrollment

Renews an existing certificate. The device must present its current certificate via mTLS.

**Request:**
```http
POST /.well-known/est/simplereenroll HTTP/1.1
Host: localhost:5000
Content-Type: application/pkcs10
Content-Transfer-Encoding: base64
# Client certificate presented via TLS

MIICVDCCATwCAQAwDzENMAsGA1UEAwwEdGVzdDCCASIwDQYJKoZIhvcNAQEB...
```

**Response:** Same as `/simpleenroll`

---

## Admin API Endpoints

All Admin API endpoints require the `X-API-Key` header.

### Health Check

```http
GET /api/status/health HTTP/1.1
Host: localhost:5000
```

**Response:**
```json
{
  "status": "Healthy",
  "components": {
    "database": "Healthy",
    "ca_connectors": "Healthy"
  }
}
```

### CA Backends

#### List CA Backends
```http
GET /api/cas HTTP/1.1
X-API-Key: your-api-key
```

**Response:**
```json
[
  {
    "id": "550e8400-e29b-41d4-a716-446655440000",
    "name": "Production CA",
    "type": "selfsigned",
    "url": null,
    "config": {
      "PfxPath": "/app/certs/ca.pfx"
    },
    "isEnabled": true,
    "createdAt": "2024-01-15T10:30:00Z",
    "updatedAt": "2024-01-15T10:30:00Z"
  }
]
```

#### Create CA Backend
```http
POST /api/cas HTTP/1.1
X-API-Key: your-api-key
Content-Type: application/json

{
  "name": "Production CA",
  "type": "selfsigned",
  "config": {
    "PfxPath": "/app/certs/ca.pfx",
    "PfxPassword": "your-password"
  },
  "isEnabled": true
}
```

**CA Backend Types and Configuration:**

| Type | Config Keys | Description |
|------|-------------|-------------|
| `selfsigned` | `PfxPath`, `PfxPassword` | PFX file containing CA cert and private key |
| `selfsigned` | `CertPath`, `KeyPath` | Separate PEM files for cert and key |
| `acme` | `DirectoryUrl`, `Email`, `PreferredChallengeType` | ACME CA (Let's Encrypt, ZeroSSL) |
| `acme` | `EabKeyId`, `EabHmacKey` | External Account Binding (for ZeroSSL) |

#### Update CA Backend
```http
PUT /api/cas/{id} HTTP/1.1
X-API-Key: your-api-key
Content-Type: application/json

{
  "name": "Updated CA Name",
  "isEnabled": false
}
```

#### Delete CA Backend
```http
DELETE /api/cas/{id} HTTP/1.1
X-API-Key: your-api-key
```

### EST Profiles

#### List EST Profiles
```http
GET /api/est-profiles HTTP/1.1
X-API-Key: your-api-key
```

**Response:**
```json
[
  {
    "id": "660e8400-e29b-41d4-a716-446655440001",
    "name": "Device Enrollment",
    "pathPrefix": "/.well-known/est",
    "hostnames": ["localhost", "est.example.com"],
    "caBackendId": "550e8400-e29b-41d4-a716-446655440000",
    "validityDays": 365,
    "requireClientCertificate": false,
    "isEnabled": true
  }
]
```

#### Create EST Profile
```http
POST /api/est-profiles HTTP/1.1
X-API-Key: your-api-key
Content-Type: application/json

{
  "name": "Device Enrollment",
  "pathPrefix": "/.well-known/est",
  "hostnames": ["localhost"],
  "caBackendId": "550e8400-e29b-41d4-a716-446655440000",
  "validityDays": 365,
  "requireClientCertificate": false,
  "isEnabled": true
}
```

### Enrollment Events

#### List Enrollment Events
```http
GET /api/status/enrollments?limit=50&profileId={profileId} HTTP/1.1
X-API-Key: your-api-key
```

**Response:**
```json
[
  {
    "id": "770e8400-e29b-41d4-a716-446655440002",
    "timestamp": "2024-01-15T14:22:00Z",
    "profileId": "660e8400-e29b-41d4-a716-446655440001",
    "deviceId": "device-001",
    "status": "issued",
    "subjectDn": "CN=device-001",
    "requestorIpAddress": "192.168.1.100",
    "errorMessage": null
  }
]
```

**Status Values:**
| Status | Description |
|--------|-------------|
| `issued` | Certificate successfully issued |
| `pending` | Waiting for CA to complete issuance |
| `rejected` | Request rejected (policy violation) |
| `error` | Error occurred during issuance |

---

## C# Client Examples

### Complete EST Client Example

```csharp
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Kryptonian.Client.Examples;

/// <summary>
/// Example EST client for enrolling certificates with Kryptonian.
/// </summary>
public class EstClient
{
    private readonly HttpClient _httpClient;
    private readonly string _estBaseUrl;

    public EstClient(string estBaseUrl, X509Certificate2? clientCertificate = null)
    {
        _estBaseUrl = estBaseUrl.TrimEnd('/');

        var handler = new HttpClientHandler();

        // For re-enrollment, add the existing client certificate
        if (clientCertificate != null)
        {
            handler.ClientCertificates.Add(clientCertificate);
        }

        // Trust the CA certificate (in production, configure properly)
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;

        _httpClient = new HttpClient(handler);
    }

    /// <summary>
    /// Gets the CA certificate chain from the EST server.
    /// </summary>
    public async Task<X509Certificate2Collection> GetCaCertificatesAsync(
        string? label = null,
        CancellationToken ct = default)
    {
        var url = string.IsNullOrEmpty(label)
            ? $"{_estBaseUrl}/.well-known/est/cacerts"
            : $"{_estBaseUrl}/.well-known/est/{label}/cacerts";

        var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var base64Content = await response.Content.ReadAsStringAsync(ct);
        var pkcs7Bytes = Convert.FromBase64String(base64Content);

        return ParsePkcs7Certificates(pkcs7Bytes);
    }

    /// <summary>
    /// Enrolls a new certificate using the EST simple enrollment endpoint.
    /// </summary>
    public async Task<X509Certificate2> EnrollAsync(
        string subjectName,
        string? label = null,
        CancellationToken ct = default)
    {
        // Generate a new key pair
        using var rsa = RSA.Create(2048);

        // Create the CSR
        var csr = new CertificateRequest(
            new X500DistinguishedName(subjectName),
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var csrBytes = csr.CreateSigningRequest();

        // Send enrollment request
        var certChain = await SendEnrollmentRequestAsync(
            csrBytes,
            "simpleenroll",
            label,
            ct);

        // The first certificate is the issued certificate
        var issuedCert = certChain[0];

        // Combine with private key
        return issuedCert.CopyWithPrivateKey(rsa);
    }

    /// <summary>
    /// Re-enrolls (renews) an existing certificate.
    /// The client must be configured with the existing certificate.
    /// </summary>
    public async Task<X509Certificate2> ReenrollAsync(
        X509Certificate2 existingCertificate,
        string? label = null,
        CancellationToken ct = default)
    {
        // Generate a new key pair for the renewed certificate
        using var rsa = RSA.Create(2048);

        // Create CSR with same subject as existing certificate
        var csr = new CertificateRequest(
            existingCertificate.SubjectName,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var csrBytes = csr.CreateSigningRequest();

        // Send re-enrollment request (requires mTLS with existing cert)
        var certChain = await SendEnrollmentRequestAsync(
            csrBytes,
            "simplereenroll",
            label,
            ct);

        var issuedCert = certChain[0];
        return issuedCert.CopyWithPrivateKey(rsa);
    }

    private async Task<X509Certificate2Collection> SendEnrollmentRequestAsync(
        byte[] csrBytes,
        string endpoint,
        string? label,
        CancellationToken ct)
    {
        var url = string.IsNullOrEmpty(label)
            ? $"{_estBaseUrl}/.well-known/est/{endpoint}"
            : $"{_estBaseUrl}/.well-known/est/{label}/{endpoint}";

        var base64Csr = Convert.ToBase64String(csrBytes);
        var content = new StringContent(base64Csr, Encoding.ASCII, "application/pkcs10");
        content.Headers.Add("Content-Transfer-Encoding", "base64");

        var response = await _httpClient.PostAsync(url, content, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.Accepted)
        {
            // Async issuance - wait and retry
            var retryAfter = response.Headers.RetryAfter?.Delta?.TotalSeconds ?? 60;
            throw new InvalidOperationException(
                $"Certificate issuance pending. Retry after {retryAfter} seconds.");
        }

        response.EnsureSuccessStatusCode();

        var base64Response = await response.Content.ReadAsStringAsync(ct);
        var pkcs7Bytes = Convert.FromBase64String(base64Response);

        return ParsePkcs7Certificates(pkcs7Bytes);
    }

    private static X509Certificate2Collection ParsePkcs7Certificates(byte[] pkcs7Bytes)
    {
        // Parse PKCS#7 SignedData to extract certificates
        var collection = new X509Certificate2Collection();
        collection.Import(pkcs7Bytes);
        return collection;
    }
}
```

### Usage Example

```csharp
// Example: Complete enrollment workflow
async Task EnrollDeviceAsync()
{
    // 1. Create EST client
    var client = new EstClient("https://est.example.com:5000");

    // 2. Get CA certificates (optional - for trust configuration)
    var caCerts = await client.GetCaCertificatesAsync();
    Console.WriteLine($"CA Subject: {caCerts[0].Subject}");

    // 3. Enroll for a new certificate
    var deviceCert = await client.EnrollAsync("CN=device-001,O=MyOrg");

    // 4. Save the certificate (example: PFX file)
    var pfxBytes = deviceCert.Export(X509ContentType.Pfx, "password");
    await File.WriteAllBytesAsync("device-001.pfx", pfxBytes);

    Console.WriteLine($"Certificate issued: {deviceCert.Subject}");
    Console.WriteLine($"Valid until: {deviceCert.NotAfter}");
}

// Example: Certificate renewal
async Task RenewCertificateAsync()
{
    // Load existing certificate
    var existingCert = new X509Certificate2("device-001.pfx", "password");

    // Create client with existing certificate for mTLS
    var client = new EstClient("https://est.example.com:5000", existingCert);

    // Re-enroll
    var renewedCert = await client.ReenrollAsync(existingCert);

    // Save renewed certificate
    var pfxBytes = renewedCert.Export(X509ContentType.Pfx, "password");
    await File.WriteAllBytesAsync("device-001-renewed.pfx", pfxBytes);

    Console.WriteLine($"Certificate renewed. New expiry: {renewedCert.NotAfter}");
}
```

### Admin API Client Example

```csharp
using System.Net.Http.Json;
using System.Text.Json;

namespace Kryptonian.Client.Examples;

/// <summary>
/// Client for the Kryptonian Admin API.
/// </summary>
public class AdminApiClient
{
    private readonly HttpClient _httpClient;

    public AdminApiClient(string baseUrl, string apiKey)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl)
        };
        _httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);
    }

    #region CA Backends

    public async Task<List<CaBackend>> GetCaBackendsAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetFromJsonAsync<List<CaBackend>>("/api/cas", ct);
        return response ?? new List<CaBackend>();
    }

    public async Task<CaBackend> CreateCaBackendAsync(CreateCaBackendRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/cas", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CaBackend>(ct)
            ?? throw new InvalidOperationException("Failed to deserialize response");
    }

    public async Task<CaBackend> CreateSelfSignedBackendAsync(
        string name,
        string pfxPath,
        string pfxPassword,
        CancellationToken ct = default)
    {
        var request = new CreateCaBackendRequest
        {
            Name = name,
            Type = "selfsigned",
            Config = new Dictionary<string, object>
            {
                ["PfxPath"] = pfxPath,
                ["PfxPassword"] = pfxPassword
            },
            IsEnabled = true
        };
        return await CreateCaBackendAsync(request, ct);
    }

    public async Task<CaBackend> CreateAcmeBackendAsync(
        string name,
        string email,
        string directoryUrl = "https://acme-v02.api.letsencrypt.org/directory",
        CancellationToken ct = default)
    {
        var request = new CreateCaBackendRequest
        {
            Name = name,
            Type = "acme",
            Url = directoryUrl,
            Config = new Dictionary<string, object>
            {
                ["Email"] = email,
                ["PreferredChallengeType"] = "http-01"
            },
            IsEnabled = true
        };
        return await CreateCaBackendAsync(request, ct);
    }

    #endregion

    #region EST Profiles

    public async Task<List<EstProfile>> GetEstProfilesAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetFromJsonAsync<List<EstProfile>>("/api/est-profiles", ct);
        return response ?? new List<EstProfile>();
    }

    public async Task<EstProfile> CreateEstProfileAsync(CreateEstProfileRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/est-profiles", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EstProfile>(ct)
            ?? throw new InvalidOperationException("Failed to deserialize response");
    }

    #endregion

    #region Health & Status

    public async Task<HealthStatus> GetHealthAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetFromJsonAsync<HealthStatus>("/api/status/health", ct);
        return response ?? new HealthStatus { Status = "Unknown" };
    }

    public async Task<List<EnrollmentEvent>> GetEnrollmentEventsAsync(
        int limit = 50,
        string? profileId = null,
        CancellationToken ct = default)
    {
        var url = $"/api/status/enrollments?limit={limit}";
        if (!string.IsNullOrEmpty(profileId))
        {
            url += $"&profileId={profileId}";
        }
        var response = await _httpClient.GetFromJsonAsync<List<EnrollmentEvent>>(url, ct);
        return response ?? new List<EnrollmentEvent>();
    }

    #endregion
}

#region DTOs

public class CaBackend
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Url { get; set; }
    public Dictionary<string, object>? Config { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateCaBackendRequest
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Url { get; set; }
    public Dictionary<string, object>? Config { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public class EstProfile
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PathPrefix { get; set; } = "/.well-known/est";
    public List<string> Hostnames { get; set; } = new();
    public string CaBackendId { get; set; } = string.Empty;
    public int ValidityDays { get; set; } = 365;
    public bool RequireClientCertificate { get; set; }
    public bool IsEnabled { get; set; }
}

public class CreateEstProfileRequest
{
    public string Name { get; set; } = string.Empty;
    public string PathPrefix { get; set; } = "/.well-known/est";
    public List<string> Hostnames { get; set; } = new();
    public string CaBackendId { get; set; } = string.Empty;
    public int ValidityDays { get; set; } = 365;
    public bool RequireClientCertificate { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public class HealthStatus
{
    public string Status { get; set; } = string.Empty;
    public Dictionary<string, string>? Components { get; set; }
}

public class EnrollmentEvent
{
    public string Id { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string ProfileId { get; set; } = string.Empty;
    public string? DeviceId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? SubjectDn { get; set; }
    public string? RequestorIpAddress { get; set; }
    public string? ErrorMessage { get; set; }
}

#endregion
```

### Complete Setup Example

```csharp
// Complete example: Set up Kryptonian and enroll a device
async Task CompleteSetupAndEnrollmentAsync()
{
    var apiKey = "your-admin-api-key";
    var baseUrl = "http://localhost:5000";

    // 1. Create Admin API client
    var adminClient = new AdminApiClient(baseUrl, apiKey);

    // 2. Check health
    var health = await adminClient.GetHealthAsync();
    Console.WriteLine($"API Status: {health.Status}");

    // 3. Create a Self-Signed CA backend
    var caBackend = await adminClient.CreateSelfSignedBackendAsync(
        name: "Device CA",
        pfxPath: "/app/certs/ca.pfx",
        pfxPassword: "TestPassword123!");
    Console.WriteLine($"Created CA Backend: {caBackend.Id}");

    // 4. Create an EST Profile
    var estProfile = await adminClient.CreateEstProfileAsync(new CreateEstProfileRequest
    {
        Name = "Device Enrollment",
        PathPrefix = "/.well-known/est",
        Hostnames = new List<string> { "localhost" },
        CaBackendId = caBackend.Id,
        ValidityDays = 365,
        RequireClientCertificate = false,
        IsEnabled = true
    });
    Console.WriteLine($"Created EST Profile: {estProfile.Id}");

    // 5. Now enroll a device
    var estClient = new EstClient(baseUrl);

    // Get CA certs first
    var caCerts = await estClient.GetCaCertificatesAsync();
    Console.WriteLine($"Got CA certificate: {caCerts[0].Subject}");

    // Enroll device
    var deviceCert = await estClient.EnrollAsync("CN=medical-device-001,O=Hospital");
    Console.WriteLine($"Device enrolled! Certificate: {deviceCert.Thumbprint}");
    Console.WriteLine($"Valid from {deviceCert.NotBefore} to {deviceCert.NotAfter}");

    // 6. Check enrollment events
    var events = await adminClient.GetEnrollmentEventsAsync(limit: 10);
    foreach (var evt in events)
    {
        Console.WriteLine($"  [{evt.Timestamp:u}] {evt.Status}: {evt.SubjectDn}");
    }
}
```

---

## OpenAPI Specifications

The OpenAPI specification files are located in the repository root:

| File | Description |
|------|-------------|
| `OpenAPI.Admin.yaml` | CA Backend management endpoints |
| `OpenAPI.ESTProfiles.yaml` | EST Profile management endpoints |
| `OpenAPI.StatusObserve.yaml` | Health check and enrollment events |
| `OpenAPI.Schema.yaml` | Shared data type definitions |

### Swagger UI

When running in Development mode, Swagger UI is available at:

```
http://localhost:5000/swagger
```

### Generating Clients

You can generate API clients using the OpenAPI specs:

```bash
# Using NSwag
nswag openapi2csclient /input:OpenAPI.Admin.yaml /output:AdminClient.cs

# Using OpenAPI Generator
openapi-generator generate -i OpenAPI.Admin.yaml -g csharp -o ./generated
```

---

## Error Handling

### HTTP Status Codes

| Code | Meaning | When |
|------|---------|------|
| `200` | Success | Request completed successfully |
| `201` | Created | Resource created (POST) |
| `202` | Accepted | Async operation started (check Retry-After) |
| `204` | No Content | Delete successful |
| `400` | Bad Request | Invalid CSR or request body |
| `401` | Unauthorized | Missing/invalid API key or client cert |
| `403` | Forbidden | Valid auth but operation not allowed |
| `404` | Not Found | Profile/backend not found |
| `413` | Payload Too Large | CSR exceeds size limit |
| `429` | Too Many Requests | Rate limit exceeded |
| `500` | Server Error | Internal error |
| `503` | Service Unavailable | CA backend unavailable |

### Error Response Format

```json
{
  "error": "Invalid CSR format",
  "details": "The provided data is not a valid PKCS#10 certificate signing request",
  "correlationId": "abc123-def456"
}
```

### Retry-After Header

When receiving a `202 Accepted` response, check the `Retry-After` header:

```http
HTTP/1.1 202 Accepted
Retry-After: 60

{"message": "Certificate issuance pending", "retryAfter": 60}
```

The client should wait the specified number of seconds before retrying the request.
