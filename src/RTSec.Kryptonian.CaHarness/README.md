# CA Harness — Agent Integration Guide

Test-only Certificate Authority service. Emulates three CA backend flavors (selfsigned, adcs, ejbca) and acts as a transparent ACME proxy in front of `step-ca`. Use it to exercise the full CA connector surface without a real Windows Server AD CS or EJBCA installation.

**Not for production.** CA private keys are generated in-memory and lost on restart.

---

## Base URL

| Context | URL |
|---|---|
| Docker Compose — from another container | `http://ca-harness:8080` |
| Docker Compose — from the host machine | `http://localhost:8090` |

All examples below use `http://localhost:8090`. Replace with `http://ca-harness:8080` when calling from inside the Docker network.

---

## Team isolation

Every team gets its own isolated CA state. All backend routes are scoped to a team ID:

```
/teams/{teamId}/api/backends/{backend}/...
/teams/{teamId}/acme/**
```

The harness creates a fresh set of backends (CA key, issued-cert store, ACME log) the first time a `teamId` is seen. One team resetting its CA has no effect on any other team. There is no pre-registration step — just start making requests with your team ID.

Teams are identified only by the path segment — choose any alphanumeric string (e.g. `team-alpha`, `team-42`).

---

## Quick Start — Verify the harness is up

```bash
curl http://localhost:8090/health
```

Expected response (`200 OK`):

```json
{ "status": "healthy", "timestamp": "2026-05-17T10:00:00Z" }
```

List available backends:

```bash
curl http://localhost:8090/api/backends
```

```json
["selfsigned", "adcs", "ejbca", "acme"]
```

List teams that have made at least one request:

```bash
curl http://localhost:8090/api/teams
```

```json
["team-alpha", "team-beta"]
```

---

## How to issue a certificate — shared steps

Every certificate request requires a PKCS#10 CSR in Base64-encoded DER format. Steps common to all backends:

**Step 1 — Set your team ID:**

```bash
TEAM=team-alpha     # use your assigned team ID
```

**Step 2 — Generate a private key and CSR** (example using `openssl`):

```bash
openssl req -newkey rsa:2048 -nodes -keyout device.key \
  -subj "/CN=linac-001/O=Hospital/C=US" \
  -out device.csr

# Convert the PEM CSR to Base64 DER for the API
openssl req -in device.csr -outform DER | base64 -w 0 > device.csr.b64
CSR=$(cat device.csr.b64)
```

**Step 3 — POST to the backend** — see backend-specific sections below.

**Step 4 — Verify the issued certificate** using the `certificatePem` field in the response:

```bash
echo "<certificatePem value>" | openssl x509 -text -noout
```

---

## Backend: selfsigned

Signs any valid CSR. No template or profile required.

**Fetch the CA certificate:**

```bash
curl http://localhost:8090/teams/$TEAM/api/backends/selfsigned/cacerts
```

Response — array of PEM strings:

```json
["-----BEGIN CERTIFICATE-----\nMIIC...\n-----END CERTIFICATE-----\n"]
```

**Issue a certificate:**

```bash
curl -s -X POST http://localhost:8090/teams/$TEAM/api/backends/selfsigned/issue \
  -H "Content-Type: application/json" \
  -d "{
    \"csrBase64Der\": \"$CSR\",
    \"validityDays\": 7,
    \"deviceId\": \"linac-001\"
  }"
```

Response (`status: "issued"`):

```json
{
  "status": "issued",
  "backend": "selfsigned",
  "issuer": "CN=SelfSigned Harness CA,O=Kryptonian Hackathon,C=US",
  "serialNumber": "1A2B3C4D",
  "thumbprint": "a3f1...",
  "notBeforeUtc": "2026-05-17T09:55:00Z",
  "notAfterUtc": "2026-05-24T09:55:00Z",
  "certificatePem": "-----BEGIN CERTIFICATE-----\n...\n-----END CERTIFICATE-----\n",
  "certificateDerBase64": "MIIC...",
  "caChainPem": ["-----BEGIN CERTIFICATE-----\n...\n-----END CERTIFICATE-----\n"],
  "message": "Certificate issued by selfsigned harness"
}
```

**Check your success (list issued certificates):**

```bash
curl http://localhost:8090/teams/$TEAM/api/backends/selfsigned/issued
```

---

## Backend: adcs (Microsoft AD CS emulator)

Requires `templateName`. The template name controls whether the request is issued, queued for approval, or rejected.

**Fetch the CA certificate:**

```bash
curl http://localhost:8090/teams/$TEAM/api/backends/adcs/cacerts
```

### Template policy

| `templateName` value | Result |
|---|---|
| `DicomDeviceAuthentication` | Issued immediately |
| `DicomBridgeMtls` | Issued immediately |
| `PendingApprovalTemplate` | Pending — requires a separate approve call |
| `RejectedTemplate` | Rejected — `reasonCode: "template-policy-rejected"` |
| Any other value | Rejected — `reasonCode: "unknown-template"` |
| *(field omitted)* | Rejected — `reasonCode: "missing-template"` |

### Issue (immediate approval)

```bash
curl -s -X POST http://localhost:8090/teams/$TEAM/api/backends/adcs/issue \
  -H "Content-Type: application/json" \
  -d "{
    \"csrBase64Der\": \"$CSR\",
    \"templateName\": \"DicomDeviceAuthentication\",
    \"validityDays\": 7,
    \"deviceId\": \"linac-001\"
  }"
```

Response (`status: "issued"`) — same shape as selfsigned, with `"backend": "adcs"`.

### Issue with pending approval (3-step flow)

**Step 1 — Submit with `PendingApprovalTemplate`:**

```bash
curl -s -X POST http://localhost:8090/teams/$TEAM/api/backends/adcs/issue \
  -H "Content-Type: application/json" \
  -d "{
    \"csrBase64Der\": \"$CSR\",
    \"templateName\": \"PendingApprovalTemplate\",
    \"validityDays\": 7,
    \"deviceId\": \"linac-001\"
  }"
```

Response (`status: "pending"`):

```json
{
  "status": "pending",
  "backend": "adcs",
  "requestId": "52a01812aae246fcb3fd5a23523da96f",
  "message": "Template PendingApprovalTemplate requires manual administrator approval. Use POST /api/backends/adcs/approve/52a01812aae246fcb3fd5a23523da96f to release."
}
```

**Step 2 — Save the `requestId` from the response.**

**Step 3 — Approve and release the certificate:**

```bash
curl -s -X POST http://localhost:8090/teams/$TEAM/api/backends/adcs/approve/52a01812aae246fcb3fd5a23523da96f
```

Response — same `status: "issued"` shape as an immediate issue. Returns `404` if the `requestId` has already been approved or does not exist.

### Rejection response shape

```json
{
  "status": "rejected",
  "backend": "adcs",
  "message": "Template RejectedTemplate is not permitted on this CA",
  "reasonCode": "template-policy-rejected"
}
```

**Check your success:**

```bash
curl http://localhost:8090/teams/$TEAM/api/backends/adcs/issued
```

---

## Backend: ejbca (EJBCA emulator)

Requires `templateName` (end-entity profile) and optionally `profileName` (certificate profile). The harness checks `templateName` first; if that is absent it falls back to `profileName`.

**Fetch the CA certificate:**

```bash
curl http://localhost:8090/teams/$TEAM/api/backends/ejbca/cacerts
```

### Profile policy

| `templateName` value | Result |
|---|---|
| `MedicalDeviceTLS` | Issued immediately |
| `DicomWebBridgeMTLS` | Issued immediately |
| `RejectedProfile` | Rejected — `reasonCode: "profile-policy-rejected"` |
| Any other value | Rejected — `reasonCode: "unknown-profile"` |
| *(field omitted)* | Rejected — `reasonCode: "missing-profile"` |

**Issue a certificate:**

```bash
curl -s -X POST http://localhost:8090/teams/$TEAM/api/backends/ejbca/issue \
  -H "Content-Type: application/json" \
  -d "{
    \"csrBase64Der\": \"$CSR\",
    \"templateName\": \"MedicalDeviceTLS\",
    \"profileName\": \"MedicalDevices\",
    \"validityDays\": 7,
    \"deviceId\": \"linac-001\"
  }"
```

Response — same `status: "issued"` shape, with `"backend": "ejbca"`.

**Check your success:**

```bash
curl http://localhost:8090/teams/$TEAM/api/backends/ejbca/issued
```

---

## Backend: acme (ACME proxy via step-ca)

The harness sits in front of `step-ca` as a transparent reverse proxy. All ACME traffic flows through the harness, which logs each significant operation. This lets you verify that a team completed the full ACME certificate issuance flow.

```
Gateway  →  ca-harness /acme/**  →  step-ca (real ACME)
                 ↓
         activity log
```

### Configure your ACME client

Point your ACME client's directory URL at the harness, **not** at step-ca directly. Include your team ID in the path:

```
http://ca-harness:8080/teams/{teamId}/acme/directory        ← inside Docker network
http://localhost:8090/teams/{teamId}/acme/directory         ← host machine
```

Example for `team-alpha`:

```
http://ca-harness:8080/teams/team-alpha/acme/directory
```

Gateway backend config:

```json
{
  "type": "acme",
  "config": {
    "DirectoryUrl": "http://ca-harness:8080/teams/team-alpha/acme/directory",
    "Email": "hackathon@example.com"
  }
}
```

Do **not** point `DirectoryUrl` at `https://step-ca:9000` directly — the proxy will not log those requests. Do **not** omit the team ID — each team's ACME activity log is stored separately.

### Check activity log

Returns every significant ACME operation for your team (HEAD nonce fetches are filtered out):

```bash
curl http://localhost:8090/teams/$TEAM/api/backends/acme/activity
```

```json
[
  { "id": "a1b2c3d4", "timestampUtc": "2026-05-17T10:01:00Z", "operation": "directory-fetch",        "method": "GET",  "path": "/acme/directory",           "statusCode": 200 },
  { "id": "e5f6a7b8", "timestampUtc": "2026-05-17T10:01:01Z", "operation": "new-account",            "method": "POST", "path": "/acme/acme/new-account",          "statusCode": 201 },
  { "id": "c9d0e1f2", "timestampUtc": "2026-05-17T10:01:02Z", "operation": "new-order",              "method": "POST", "path": "/acme/acme/new-order",            "statusCode": 201 },
  { "id": "a3b4c5d6", "timestampUtc": "2026-05-17T10:01:04Z", "operation": "challenge-response",     "method": "POST", "path": "/acme/acme/challenge/xyz/http-01","statusCode": 200 },
  { "id": "e7f8a9b0", "timestampUtc": "2026-05-17T10:01:06Z", "operation": "order-finalized",        "method": "POST", "path": "/acme/acme/order/abc/finalize",   "statusCode": 200 },
  { "id": "c1d2e3f4", "timestampUtc": "2026-05-17T10:01:08Z", "operation": "certificate-downloaded", "method": "POST", "path": "/acme/acme/cert/def",             "statusCode": 200 }
]
```

Logged operations:

| Operation | When it appears |
|---|---|
| `directory-fetch` | Client fetched the ACME directory |
| `new-account` | ACME account created or looked up |
| `new-order` | Certificate order placed |
| `authorization` | Authorization object fetched |
| `challenge-response` | Challenge submitted |
| `order-status` | Order status polled |
| `order-finalized` | CSR submitted (finalize) |
| `certificate-downloaded` | Certificate chain downloaded ✓ |

### Check pass/fail

Returns only `certificate-downloaded` entries with `statusCode: 200` for your team. An empty array means ACME has not been completed.

```bash
curl http://localhost:8090/teams/$TEAM/api/backends/acme/issued
```

A team has **passed** ACME if this returns at least one record.

---

## Revoke a certificate

Works for `selfsigned`, `adcs`, and `ejbca`. Use the `serialNumber` from the issue response.

```bash
curl -s -X POST http://localhost:8090/teams/$TEAM/api/backends/adcs/revoke \
  -H "Content-Type: application/json" \
  -d "{
    \"serialNumber\": \"1A2B3C4D\",
    \"reason\": \"KeyCompromise\"
  }"
```

Response:

```json
{ "revoked": true, "serialNumber": "1A2B3C4D" }
```

Returns `404` if the serial was not issued by this backend.

---

## Inspect issued certificates

Returns the full history for a backend, including revocation status:

```bash
curl http://localhost:8090/teams/$TEAM/api/backends/adcs/issued
```

```json
[
  {
    "serialNumber": "1A2B3C4D",
    "thumbprint": "a3f1...",
    "subjectDn": "CN=linac-001",
    "certificatePem": "-----BEGIN CERTIFICATE-----\n...",
    "certificateDerBase64": "MIIC...",
    "notBeforeUtc": "2026-05-17T09:55:00Z",
    "notAfterUtc": "2026-05-24T09:55:00Z",
    "isRevoked": false
  }
]
```

---

## Reset a backend

Regenerates the CA key and clears all issued certificates for that backend. Only affects your team's state — other teams are not affected.

```bash
# Reset selfsigned
curl -X POST http://localhost:8090/teams/$TEAM/api/backends/selfsigned/reset

# Reset adcs
curl -X POST http://localhost:8090/teams/$TEAM/api/backends/adcs/reset

# Reset ejbca
curl -X POST http://localhost:8090/teams/$TEAM/api/backends/ejbca/reset

# Reset acme activity log
curl -X POST http://localhost:8090/teams/$TEAM/api/backends/acme/reset
```

Response:

```json
{ "backend": "adcs", "reset": true, "timestamp": "2026-05-17T10:00:00Z" }
```

Run these before each demo run to start a team clean. Each team only needs to reset their own state; they cannot affect other teams.

---

## Gateway connector configuration

Add a `CaBackend` row via the gateway admin API for each harness backend you want the gateway to use. Replace `{teamId}` with your team ID (e.g. `team-alpha`).

### ADCS

```json
{
  "name": "ADCS Harness",
  "type": "adcs",
  "url": "http://ca-harness:8080",
  "config": {
    "HarnessBaseUrl": "http://ca-harness:8080/teams/{teamId}",
    "TemplateName": "DicomDeviceAuthentication",
    "ValidityDays": 7
  },
  "isEnabled": true
}
```

### EJBCA

```json
{
  "name": "EJBCA Harness",
  "type": "ejbca",
  "url": "http://ca-harness:8080",
  "config": {
    "HarnessBaseUrl": "http://ca-harness:8080/teams/{teamId}",
    "CertificateProfile": "MedicalDeviceTLS",
    "EndEntityProfile": "DicomDevice",
    "ValidityDays": 7
  },
  "isEnabled": true
}
```

### ACME

```json
{
  "name": "ACME Harness",
  "type": "acme",
  "url": "http://ca-harness:8080",
  "config": {
    "DirectoryUrl": "http://ca-harness:8080/teams/{teamId}/acme/directory",
    "Email": "hackathon@example.com"
  },
  "isEnabled": true
}
```

### Self-signed

The self-signed backend is handled by the gateway's built-in `SelfSignedCaConnector`. It does not use the harness. Configure it with a PFX or PEM certificate as normal.

---

## Running the stack

```powershell
# Harness only (with host port 8090 exposed for manual testing)
docker compose -f docker-compose.yml -f docker-compose.hackathon.yml up ca-harness --build

# Full hackathon stack (gateway + harness + step-ca + simulated devices)
docker compose -f docker-compose.yml -f docker-compose.hackathon.yml --profile web up --build
```

The harness is reachable at `http://localhost:8090` from the host and `http://ca-harness:8080` from other containers on `kryptonian-network`.

### Environment variables (ca-harness container)

| Variable | Default | Purpose |
|---|---|---|
| `ACME_UPSTREAM_URL` | `https://step-ca:9000` | step-ca URL the ACME proxy forwards to |
| `ACME_HARNESS_BASE_URL` | *(derived from request `Host` header)* | Harness public URL written into the rewritten ACME directory |

---

## All endpoints at a glance

| Method | Path | Description |
|---|---|---|
| `GET` | `/health` | Liveness check |
| `GET` | `/api/backends` | List backend IDs |
| `GET` | `/api/teams` | List teams that have made at least one request |
| `GET` | `/teams/{teamId}/api/backends/{backend}/cacerts` | CA certificate chain (PEM array) |
| `POST` | `/teams/{teamId}/api/backends/{backend}/issue` | Issue a certificate |
| `POST` | `/teams/{teamId}/api/backends/{backend}/revoke` | Revoke by serial number |
| `GET` | `/teams/{teamId}/api/backends/{backend}/issued` | List all issued certificates |
| `POST` | `/teams/{teamId}/api/backends/{backend}/reset` | Reset CA key and issued list (team-scoped) |
| `POST` | `/teams/{teamId}/api/backends/adcs/approve/{requestId}` | Release a pending ADCS request |
| `GET` | `/teams/{teamId}/api/backends/acme/activity` | Full ACME operation log |
| `ANY` | `/teams/{teamId}/acme/**` | Transparent proxy to step-ca |

Valid `{backend}` values: `selfsigned`, `adcs`, `ejbca`, `acme` (where applicable).
