# Azure CA Backend Harness Plan

## Summary

Build a hackathon-ready **Certificate Authority Harness** that can be hosted in Azure and run locally with Docker Compose.

The harness is test infrastructure. It is not the system under test. Its job is to let the Certificate Enrollment Gateway prove that one device-side enrollment workflow can route to different hospital-style CA backends.

The gateway remains responsible for medical-device trust policy:

- Device activation
- Rejection before activation
- Trust removal
- Renewal authorization
- CA profile selection
- Audit trail

The CA harness is responsible for acting like different CA backend types:

- Self-signed lab CA
- ACME-compatible private CA
- Microsoft ADCS-style CA
- EJBCA-style enterprise CA

## Core Decisions

- Use **Azure Container Apps** as the shared cloud deployment target.
- Use **Docker Compose** for local development and repeatable team demos.
- Use a **hybrid realism model**:
  - Existing self-signed CA behavior for baseline testing.
  - Real `step-ca` container for ACME.
  - Emulator mode for Microsoft ADCS.
  - Emulator mode for EJBCA.
- Keep real Microsoft ADCS as a stretch deployment using a Windows Server VM.
- Keep real EJBCA container integration as a stretch goal after the emulator contract is stable.
- Keep activation and trust decisions in the Certificate Enrollment Gateway, not in the CA harness.

## Target Architecture

```text
Medical Device Simulator
    -> EST
    -> Certificate Enrollment Gateway
        -> Self-signed connector
        -> ACME connector
            -> step-ca
        -> ADCS harness connector
            -> ca-harness / adcs
        -> EJBCA harness connector
            -> ca-harness / ejbca
```

The gateway should call each backend through the existing CA connector abstraction. The device should not know which CA backend is active.

## Local Services

Add a hackathon Docker Compose overlay that can start:

```text
postgres
gateway-api
gateway-web
ca-harness
step-ca
device-a
device-b
```

Optional later services:

```text
dicomweb-bridge-a
dicomweb-bridge-b
dicom-receiver
dicom-sender
```

Recommended command:

```powershell
docker compose -f docker-compose.yml -f docker-compose.hackathon.yml --profile web up --build
```

## Azure Services

Create one isolated Azure environment per team.

Example:

```text
rg-kryptonian-hackathon-team-a
rg-kryptonian-hackathon-team-b
rg-kryptonian-hackathon-shared
```

Shared resources:

```text
Azure Container Registry
sample DICOM storage
optional shared DNS zone
baseline deployment scripts
```

Per-team resources:

```text
Azure Container Apps environment
Azure Database for PostgreSQL Flexible Server
kryptonian-api container app
kryptonian-web container app
ca-harness container app
step-ca container app
```

Ingress rules:

- `kryptonian-api`: external HTTPS.
- `kryptonian-web`: external HTTPS.
- `ca-harness`: internal only.
- `step-ca`: internal only by default.

If ACME HTTP-01 validation requires external reachability, expose only the challenge route needed for the test and document that exception.

## CA Harness API

The `ca-harness` service should expose a small REST API.

Required endpoints:

```text
GET  /health
GET  /api/backends
POST /api/backends/{backend}/reset
GET  /api/backends/{backend}/cacerts
POST /api/backends/{backend}/issue
POST /api/backends/{backend}/revoke
GET  /api/backends/{backend}/issued
```

Supported backend values for v1:

```text
selfsigned
adcs
ejbca
```

ACME is handled separately by `step-ca` through the gateway's ACME connector.

## Issue Request Contract

`POST /api/backends/{backend}/issue`

Request:

```json
{
  "csrBase64Der": "MIIC...",
  "profileName": "MedicalDevices",
  "templateName": "DicomDeviceAuthentication",
  "validityDays": 7,
  "subjectOverride": null,
  "deviceId": "linac-001",
  "metadata": {
    "manufacturer": "ExampleMed",
    "serialNumber": "SN-12345",
    "requestedBy": "gateway"
  }
}
```

Response:

```json
{
  "status": "issued",
  "backend": "adcs",
  "issuer": "CN=ADCS Harness CA",
  "serialNumber": "00A1B2C3",
  "thumbprint": "123456...",
  "notBeforeUtc": "2026-05-17T00:00:00Z",
  "notAfterUtc": "2026-05-24T00:00:00Z",
  "certificatePem": "-----BEGIN CERTIFICATE-----...",
  "certificateDerBase64": "MIIC...",
  "caChainPem": [
    "-----BEGIN CERTIFICATE-----..."
  ],
  "message": "Certificate issued by ADCS emulator"
}
```

Failure response:

```json
{
  "status": "rejected",
  "backend": "adcs",
  "message": "Template DicomDeviceAuthentication does not allow this subject",
  "reasonCode": "template-policy-rejected"
}
```

## Backend Behavior

### Self-Signed Harness Mode

Purpose:

- Fast baseline CA behavior.
- Deterministic local tests.
- No external services.

Behavior:

- Generate or load a local root CA.
- Sign incoming CSRs.
- Return leaf certificate and CA chain.
- Support reset by regenerating the root CA.
- Support revoke by recording revoked serials.

### ACME / step-ca Mode

Purpose:

- Prove the gateway can talk to an ACME-compatible CA.
- Avoid public DNS and public CA dependency during the hackathon.

Behavior:

- Run `step-ca` as a local/private ACME CA.
- Configure an ACME provisioner.
- Point the gateway ACME backend `DirectoryUrl` to the internal `step-ca` ACME directory.
- Use short-lived test certificates.
- Use internal DNS names appropriate for the container network.

Notes:

- The gateway should use the existing ACME connector path.
- The harness does not need to proxy ACME unless a later test needs uniform reporting.
- Pebble can be added later for ACME client conformance testing.

### Microsoft ADCS Emulator Mode

Purpose:

- Emulate the hospital enterprise PKI pattern most likely to matter in Windows environments.
- Avoid making Windows Server AD CS a hackathon blocker.

Behavior:

- Expose ADCS-like concepts:
  - CA name
  - certificate template
  - request disposition
  - issued, pending, and rejected results
- Accept a CSR and template name.
- Reject unknown templates.
- Optionally mark some templates as requiring approval.
- Return issuer name such as `CN=ADCS Harness CA`.

Required demo templates:

```text
DicomDeviceAuthentication
DicomBridgeMtls
RejectedTemplate
PendingApprovalTemplate
```

Stretch:

- Add a real ADCS connector that talks to a Windows Server AD CS lab VM through a REST shim, PowerShell bridge, or `certreq` automation.

### EJBCA Emulator Mode

Purpose:

- Emulate an enterprise CA with CA names, certificate profiles, and end-entity profiles.
- Prove the gateway is not hardcoded to ADCS concepts.

Behavior:

- Expose EJBCA-like concepts:
  - CA name
  - certificate profile
  - end-entity profile
  - username or device identity
- Accept a CSR and profile names.
- Reject unknown profiles.
- Return issuer name such as `CN=EJBCA Harness CA`.

Required demo profiles:

```text
MedicalDeviceTLS
DicomWebBridgeMTLS
RejectedProfile
```

Stretch:

- Add real EJBCA Community container integration using EJBCA's REST API.

## Gateway Integration

The gateway already has an `ICaConnector` abstraction and `CaBackendType` values for planned CA backends.

Implementation should add hackathon connectors for:

```text
Adcs
Ejbca
```

These connectors should:

- Read the harness base URL from CA backend config.
- Send CSR bytes to the harness issue endpoint.
- Convert harness responses into `CertificateIssuanceResult`.
- Return the harness CA chain from `GetCaCertificatesAsync`.
- Map harness rejection into clear enrollment failure messages.
- Implement revoke by calling the harness revoke endpoint.
- Implement `TestConnectionAsync` using `/health`.

Recommended backend config examples:

```json
{
  "type": "adcs",
  "url": "http://ca-harness:8080",
  "config": {
    "Backend": "adcs",
    "TemplateName": "DicomDeviceAuthentication"
  }
}
```

```json
{
  "type": "ejbca",
  "url": "http://ca-harness:8080",
  "config": {
    "Backend": "ejbca",
    "CertificateProfile": "MedicalDeviceTLS",
    "EndEntityProfile": "DicomDevice"
  }
}
```

## Demo Matrix

The same gateway scenario should run against each backend:

| Backend | Required For V1 | Notes |
|---------|-----------------|-------|
| Self-signed | Yes | Fast baseline |
| ACME / step-ca | Yes | Real ACME-compatible private CA |
| ADCS emulator | Yes | Microsoft-style hospital PKI behavior |
| EJBCA emulator | Yes | Enterprise CA profile behavior |
| Real ADCS VM | Stretch | Most credible Microsoft demo |
| Real EJBCA container | Stretch | Real enterprise CA integration |
| Vault PKI | Stretch | Modern secrets/PKI backend |
| OpenXPKI | Stretch | Open-source trustcenter backend |

## Acceptance Scenarios

Run these scenarios for every v1 backend.

1. Gateway can load the backend CA chain.
2. Unknown device enrollment is rejected by gateway policy.
3. Pending device is visible to an administrator.
4. Activated device enrollment succeeds.
5. Issued certificate chains to the selected backend CA.
6. Certificate serial and thumbprint are recorded.
7. Renewal produces a new certificate.
8. Untrusted device renewal is denied by the gateway.
9. DICOM transfer succeeds while certificates are valid.
10. DICOM transfer fails after trust removal or certificate expiry.
11. Audit output identifies backend type, issuer, serial, device ID, and result.

## Harness Tests

Unit and integration tests should cover:

- `/health` reports ready state.
- `/api/backends` lists selfsigned, adcs, and ejbca.
- `/reset` regenerates only the requested backend.
- `/cacerts` returns the expected CA chain.
- `/issue` rejects malformed CSRs.
- `/issue` signs valid CSRs.
- ADCS emulator rejects unknown templates.
- ADCS emulator supports pending approval behavior.
- EJBCA emulator rejects unknown profiles.
- EJBCA emulator signs with the expected issuer.
- `/revoke` records revoked serials.
- `/issued` returns issued certificate history.

## Azure Tests

Azure deployment should prove:

- Team environments are isolated.
- Gateway can reach internal `ca-harness`.
- Gateway can reach internal `step-ca`.
- External users can reach only gateway API and web endpoints.
- Secrets are not printed in container logs.
- The full acceptance scenario can be rerun after a container restart.
- The environment can be reset for the next team/demo run.

## Security Constraints

- The harness is for hackathon and test use only.
- Harness CA private keys are test secrets, not production CA keys.
- Do not expose `ca-harness` publicly.
- Do not expose `step-ca` publicly unless required for a controlled ACME challenge test.
- Do not use public patient data in sample DICOM files.
- Do not treat MAC address or IP address as primary device identity.
- Keep device activation and trust removal in the gateway.

## Implementation Order

1. Create `ca-harness` service with self-signed, ADCS emulator, and EJBCA emulator modes.
2. Add local Docker Compose overlay for hackathon services.
3. Add gateway harness connectors for `Adcs` and `Ejbca`.
4. Add `step-ca` service and configure gateway ACME backend against it.
5. Add acceptance-test runner that executes the same scenario against each backend.
6. Add Azure Container Apps deployment scripts per team.
7. Add reset script for demos.
8. Add stretch real ADCS VM connector only after the emulator flow works.

## Open Questions For Implementation

- Whether the harness should be its own .NET project in this repo or a small separate service.
- Whether ADCS pending approval should be simulated through a manual admin endpoint or automatic timeout.
- Whether `step-ca` should use HTTP-01, DNS-01, or an internal ACME mode for the first demo.
- Whether the DICOM transfer acceptance runner should live with the gateway tests or in a separate hackathon demo project.

## References

- Azure Container Apps: https://learn.microsoft.com/azure/container-apps/
- Azure Container Registry: https://learn.microsoft.com/azure/container-registry/
- Azure Database for PostgreSQL Flexible Server: https://learn.microsoft.com/azure/postgresql/flexible-server/
- Azure Container Apps client certificate support: https://learn.microsoft.com/azure/container-apps/client-certificate-authorization
- Smallstep `step-ca` ACME basics: https://smallstep.com/docs/step-ca/acme-basics
- Pebble ACME test server: https://github.com/letsencrypt/pebble
- EJBCA: https://www.ejbca.org/

