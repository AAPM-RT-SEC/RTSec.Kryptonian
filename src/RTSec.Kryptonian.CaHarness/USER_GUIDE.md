# CA Harness — API Reference

**Base URL:** `https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io`

📥 **[Download DICOM test files (28 MB)](https://stkryptonianfiles.blob.core.windows.net/downloads/dicom-examples.zip)** — 138 CT instances. Use these as the payload for your DIMSE and DICOMweb submissions.

🏆 **[Hackathon Scoring Guide](https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/api/help)** · **[Live Leaderboard](https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/scoreboard)**

---

## Register your team

Every team has isolated, private state. Register once to get a secret token, then use it in every request path. The leaderboard shows only your team name — never the token.

```bash
curl -s -X POST https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/api/teams/register \
  -H "Content-Type: application/json" \
  -d '{"teamName": "Team Alpha"}'
```

Response:
```json
{ "token": "a3f1b2c4-1234-5678-abcd-ef0123456789", "teamName": "Team Alpha" }
```

Save the token — it cannot be retrieved. All subsequent requests use:
```
/teams/{token}/...
```

---

## Available `{backend}` values

Use these strings wherever a URL contains `{backend}`:

| Value | CA type | Enrollment protocol | Endpoint |
|---|---|---|---|
| `selfsigned` | Self-signed CA | EST | `/teams/{token}/est/selfsigned/simpleenroll` |
| `adcs` | Microsoft AD CS | SCEP | `/teams/{token}/scep/adcs?operation=...` |
| `ejbca` | EJBCA | EJBCA REST API | `/teams/{token}/ejbca/ejbca-rest-api/v1/certificate/pkcs10enroll` |
| `acme` | ACME via step-ca | ACME | `/teams/{token}/acme/directory` |

**EST backend:** `selfsigned` only.  
**SCEP backend:** `adcs` — use the SCEP endpoint, not EST.  
**EJBCA REST backend:** `ejbca` — use the EJBCA REST endpoint, not EST.  
**ACME backend:** configure your gateway's `DirectoryUrl` to `/teams/{token}/acme/directory`. Order certs for `ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io` and use the HTTP-01 challenge relay (see ACME section below).

---

## Scoring overview

Points come from three automated flows and one manual judged category. **All automated flows require your gateway to use the enrollment protocol for each backend** — certificates from the `/issue` JSON API do not earn gateway credit.

| Category | What it tests | Points |
|---|---|---|
| Flow 1 — DIMSE mTLS C-STORE | Gateway-enrolled cert → mTLS C-STORE to DIMSE proxy | 1 per CA backend (4 max) |
| Flow 2 — C-MOVE → DICOMWeb | C-MOVE from Orthanc → your SCP → STOW-RS to harness | 1 |
| Flow 3 — DICOMWeb → DIMSE | WADO-RS retrieve from Orthanc → C-STORE back to Orthanc | 1 |
| UI Demo — Device registration | Gateway UI registers devices; unregistered devices are rejected | 1 |
| UI Demo — Pending status | Registered devices start in a pending state requiring approval | 1 |
| UI Demo — Device removal | Removing a device blocks certificate auto-renewal | 1 |
| **Total** | | **9** |

The three UI Demo points are awarded by judges during a live gateway demonstration. See [UI Demo scoring](#ui-demo-scoring) below.

---

## EST enrollment — `selfsigned` backend only

To earn gateway credit for the `selfsigned` backend your device certificates **must** be issued via the EST endpoint. This embeds a private OID (`1.3.6.1.4.1.99999.1`) that is verified at scoring time. Certificates from `/issue` never carry this OID.

**Enroll a new device:**
```bash
CSR=$(openssl req -newkey rsa:2048 -nodes -keyout device.key \
  -subj "/CN=my-device/O=Hospital/C=US" -outform DER 2>/dev/null | base64 -w 0)

curl -s -X POST https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{token}/est/selfsigned/simpleenroll \
  -H "Content-Type: application/pkcs10" \
  -H "X-Device-Id: my-device-001" \
  --data "$CSR"
```

**Renew a device certificate:**
```bash
curl -s -X POST https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{token}/est/selfsigned/simplereenroll \
  -H "Content-Type: application/pkcs10" \
  -H "X-Device-Id: my-device-001" \
  --data "$CSR"
```

Both return a standard `IssueResponse` JSON with `certificatePem` and `certificateDerBase64`.

> **Note:** The `adcs` and `ejbca` backends no longer accept EST. Use SCEP for `adcs` and the EJBCA REST API for `ejbca` (see sections below).

---

## SCEP enrollment — `adcs` backend

The ADCS backend uses SCEP (Simple Certificate Enrollment Protocol). Three operations are supported via query parameter.

**Get CA certificate** (degenerate PKCS#7, `application/x-x509-ca-cert`):
```bash
curl -s "https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{token}/scep/adcs?operation=GetCACert" \
  -o adcs-ca.p7
```

**Get CA capabilities** (text list):
```bash
curl -s "https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{token}/scep/adcs?operation=GetCACaps"
# Returns: SHA-256\nAES\nPOSTPKIOperation
```

**PKI operation** (enroll — POST with SCEP PKCSReq as DER `application/x-pki-message`):
```bash
curl -s -X POST \
  "https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{token}/scep/adcs?operation=PKIOperation" \
  -H "Content-Type: application/x-pki-message" \
  -H "X-Device-Id: my-device-001" \
  --data-binary @scep-request.bin \
  -o scep-response.bin
```

The SCEP response is a signed `CertRep` PKCS#7. The issued certificate embeds OID `1.3.6.1.4.1.99999.2` (SCEP enrollment marker) for gateway credit.

---

## EJBCA REST enrollment — `ejbca` backend

The EJBCA backend uses the EJBCA REST API. Send a JSON body with a PEM-encoded PKCS#10 CSR.

**Enroll a new device:**
```bash
# Generate CSR
openssl req -newkey rsa:2048 -nodes -keyout device.key \
  -subj "/CN=my-device/O=Hospital/C=US" -out device.csr

CSR_PEM=$(cat device.csr)

curl -s -X POST \
  "https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{token}/ejbca/ejbca-rest-api/v1/certificate/pkcs10enroll" \
  -H "Content-Type: application/json" \
  -d '{
    "certificate_request": "'"$CSR_PEM"'",
    "certificate_profile_name": "MedicalDeviceTLS",
    "username": "my-device-001"
  }'
```

**Allowed `certificate_profile_name` values:**

| Value | Result |
|---|---|
| `MedicalDeviceTLS` | Issued |
| `DicomWebBridgeMTLS` | Issued |
| `RejectedProfile` | 422 Unprocessable Entity |
| Any other / omitted | 422 Unprocessable Entity |

**Response (200 OK):**
```json
{
  "certificate": "MIIC...",
  "serial_number": "1A2B3C...",
  "response_type": "CERTIFICATE"
}
```

The `certificate` field is base64-encoded DER. Use it as the `X-Device-Certificate` header in DICOM STOW-RS submissions. The issued certificate embeds OID `1.3.6.1.4.1.99999.3` (EJBCA REST enrollment marker) for gateway credit.

---

## Flow 1 — DIMSE mTLS C-STORE

The DIMSE TLS proxy sits at **`kryptonian-dimse.eastus.cloudapp.azure.com:4243`**. It accepts DICOM C-STORE connections with mTLS. It validates your client certificate against all team CAs and automatically records the score.

**Step 1 — Trust the proxy server certificate:**
```bash
curl http://kryptonian-dimse.eastus.cloudapp.azure.com:8044/server-cert -o dimse-proxy.pem
# Add dimse-proxy.pem to your DICOM client trust store
```

**Step 2 — Enroll your device** using the protocol for your target backend:
- `selfsigned`: EST simpleenroll
- `adcs`: SCEP PKIOperation
- `ejbca`: EJBCA REST pkcs10enroll
- `acme`: ACME (DirectoryUrl)

See the per-backend enrollment sections above.

**Step 3 — C-STORE to the proxy:**

Configure your DICOM client/library to:
- Server: `kryptonian-dimse.eastus.cloudapp.azure.com:4243`
- TLS: enabled, server cert = `dimse-proxy.pem`
- Client cert: the certificate from your EST enrollment (PEM or PFX)
- Called AET: `KRYPTONIAN`

Perform a C-STORE of any file from the test DICOM set. The proxy identifies your team from the certificate, records the event, and 🔒 appears on the leaderboard for that backend.

**Repeat for each CA backend** to earn all four 🔒 badges.

---

## Flow 2 — C-MOVE → DICOMWeb

After Flow 1, the DICOM file you C-STOREd is in Orthanc. Your gateway now retrieves it via C-MOVE and submits it to the harness via DICOMweb.

**Orthanc plain DIMSE:** `kryptonian-dimse.eastus.cloudapp.azure.com:4242` (no TLS, AET: `KRYPTONIAN`)

**Step 1 — Send a C-MOVE request** to Orthanc asking it to push to your SCP. Your SCP receives the file.

**Step 2 — STOW-RS to the harness:**
```bash
curl -s -X POST https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{token}/dicom/backends/{backend}/stow \
  -H "X-Device-Certificate: $CERT_DER_B64" \
  -H "X-Transfer-Mode: cmove" \
  -H "Content-Type: application/dicom" \
  --data-binary @received-file.dcm
```

`X-Device-Certificate` must be the `certificateDerBase64` from your gateway enrollment (base64 DER — **not PEM**, which has newlines that are invalid in headers). For EST this is `certificateDerBase64`. For EJBCA REST this is the `certificate` field. For SCEP, decode the CertRep and extract the issued cert DER.

---

## Flow 3 — DICOMWeb → DIMSE

Retrieve a DICOM file from Orthanc via WADO-RS, then C-STORE it back via plain DIMSE.

**Step 1 — WADO-RS retrieve from Orthanc DICOMweb:**
```bash
# List studies
curl http://kryptonian-dimse.eastus.cloudapp.azure.com:8042/dicom-web/studies

# Retrieve instances in a study
curl http://kryptonian-dimse.eastus.cloudapp.azure.com:8042/dicom-web/studies/{studyUID}/instances \
  -o retrieved.dcm
```

**Step 2 — C-STORE back to Orthanc** at `kryptonian-dimse.eastus.cloudapp.azure.com:4242` (plain DIMSE, no TLS).

**Step 3 — Claim the score:**
```bash
curl -s -X POST https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{token}/scoring/dicomweb-to-dimse \
  -H "Content-Type: application/json" \
  -d '{"sopInstanceUid": "<uid-of-the-instance-you-stored>"}'
```

The harness verifies the UID exists in Orthanc and awards the point.

---

## CA Backends

| Backend | What it emulates | Gateway enrollment | Issue requirement |
|---|---|---|---|
| `selfsigned` | Generic self-signed CA | EST | none |
| `adcs` | Microsoft AD CS | SCEP | none for SCEP; `templateName` for `/issue` JSON path |
| `ejbca` | EJBCA | EJBCA REST | `certificate_profile_name` required |
| `acme` | ACME (step-ca) | ACME | `DirectoryUrl` in gateway config |

### selfsigned

Signs any valid CSR. No template required.

```bash
curl -s -X POST https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{token}/api/backends/selfsigned/issue \
  -H "Content-Type: application/json" \
  -d '{"csrBase64Der": "'"$CSR"'", "validityDays": 7, "deviceId": "my-device"}'
```

### adcs

Requires `templateName`. Allowed values:

| `templateName` | Result |
|---|---|
| `DicomDeviceAuthentication` | Issued |
| `DicomBridgeMtls` | Issued |
| `PendingApprovalTemplate` | Pending — call approve endpoint to release |
| `RejectedTemplate` | Rejected |
| Any other / omitted | Rejected |

```bash
curl -s -X POST https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{token}/api/backends/adcs/issue \
  -H "Content-Type: application/json" \
  -d '{"csrBase64Der": "'"$CSR"'", "templateName": "DicomDeviceAuthentication", "validityDays": 7}'
```

Pending approval flow:
```bash
curl -s -X POST https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{token}/api/backends/adcs/approve/{requestId}
```

### ejbca

Requires `templateName`. Allowed values:

| `templateName` | Result |
|---|---|
| `MedicalDeviceTLS` | Issued |
| `DicomWebBridgeMTLS` | Issued |
| `RejectedProfile` | Rejected |
| Any other / omitted | Rejected |

```bash
curl -s -X POST https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{token}/api/backends/ejbca/issue \
  -H "Content-Type: application/json" \
  -d '{"csrBase64Der": "'"$CSR"'", "templateName": "MedicalDeviceTLS", "validityDays": 7}'
```

### acme

The ACME backend uses [step-ca](https://smallstep.com/docs/step-ca/) proxied through the harness. Because ACME challenge validation requires a publicly reachable HTTP endpoint, the harness acts as the challenge responder on your behalf. Your ACME order domain is the harness public hostname.

**Step 1 — Configure your ACME client DirectoryUrl:**

```
https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{token}/acme/directory
```

Do **not** point directly at step-ca — the harness will not log your team's activity.

**Step 2 — Order a certificate for the harness hostname:**

Use `ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io` as the domain (CN/SAN) in your ACME order. This is the domain the harness can prove ownership of for HTTP-01 challenge validation.

**Step 3 — Register the HTTP-01 challenge before validation:**

Your ACME client will receive a challenge `token` and `keyAuthorization` from step-ca. Before calling step-ca to validate, POST them to the harness:

```bash
curl -s -X POST \
  https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{token}/acme/challenge \
  -H "Content-Type: application/json" \
  -d '{"token": "<acme-challenge-token>", "keyAuth": "<key-authorization>"}'
```

The harness stores this and serves it at `GET /.well-known/acme-challenge/{acme-challenge-token}` when step-ca calls to validate.

**Step 4 — Tell step-ca to validate** (via your ACME client). step-ca calls the harness, the challenge passes, and the cert is issued.

**Step 5 — Claim the cert for scoring:**

```bash
curl -s -X POST \
  https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{token}/api/backends/acme/claim \
  -H "Content-Type: application/json" \
  -d '{"certificate": "-----BEGIN CERTIFICATE-----\n...\n-----END CERTIFICATE-----"}'
```

This awards the ACME backend point on the leaderboard.

```bash
# Check ACME protocol activity
curl https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{token}/api/backends/acme/activity

# Check claimed certs (must have at least one for ACME score)
curl https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{token}/api/backends/acme/issued
```

> **Why this approach?** ACME HTTP-01 validation requires the domain in your order to publicly resolve to a server you control. The harness hostname does resolve to the harness — so the harness acts as your challenge responder. Your ACME client handles the full RFC 8555 protocol flow (account, order, challenges, finalize, download); only the challenge response is delegated to the harness.

---

## Prepare a CSR (direct issue path)

These are for testing only — use the EST path for scoring.

```bash
openssl req -newkey rsa:2048 -nodes -keyout device.key \
  -subj "/CN=my-device/O=Hospital/C=US" -out device.csr

CSR=$(openssl req -in device.csr -outform DER | base64 -w 0)
```

---

## Response shapes

**Issued:**
```json
{
  "status": "issued",
  "backend": "adcs",
  "serialNumber": "1A2B3C4D",
  "thumbprint": "a3f1...",
  "notBeforeUtc": "2026-05-18T10:00:00Z",
  "notAfterUtc": "2026-05-25T10:00:00Z",
  "certificatePem": "-----BEGIN CERTIFICATE-----\n...",
  "certificateDerBase64": "MIIC...",
  "caChainPem": ["-----BEGIN CERTIFICATE-----\n..."]
}
```

**Rejected:**
```json
{ "status": "rejected", "backend": "adcs", "message": "...", "reasonCode": "unknown-template" }
```

**Pending (adcs only):**
```json
{ "status": "pending", "backend": "adcs", "requestId": "52a01812..." }
```

---

## Revoke a certificate

```bash
curl -s -X POST https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{token}/api/backends/adcs/revoke \
  -H "Content-Type: application/json" \
  -d '{"serialNumber": "1A2B3C4D", "reason": "KeyCompromise"}'
```

---

## Reset your team state

Clears issued certificates and regenerates the CA key for one backend.

```bash
curl -X POST https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{token}/api/backends/{backend}/reset
```

---

## All endpoints

| Method | Path | Description |
|---|---|---|
| `GET` | `/` | Hackathon landing page |
| `GET` | `/health` | Service health check |
| `GET` | `/api/backends` | List backend IDs |
| `POST` | `/api/teams/register` | Register team, returns private token |
| `GET` | `/api/scoreboard` | JSON scoreboard |
| `GET` | `/scoreboard` | Live HTML leaderboard |
| `GET` | `/api/help` | This guide |
| `GET` | `/teams/{token}/api/backends/{backend}/cacerts` | CA certificate chain |
| `POST` | `/teams/{token}/api/backends/{backend}/issue` | Issue cert (no gateway credit) |
| `POST` | `/teams/{token}/api/backends/{backend}/revoke` | Revoke by serial number |
| `GET` | `/teams/{token}/api/backends/{backend}/issued` | List issued certificates |
| `POST` | `/teams/{token}/api/backends/{backend}/reset` | Reset CA state |
| `POST` | `/teams/{token}/api/backends/adcs/approve/{requestId}` | Release pending ADCS request |
| `GET` | `/teams/{token}/api/backends/acme/activity` | ACME operation log |
| `POST` | `/teams/{token}/est/selfsigned/simpleenroll` | **EST enrollment** — `selfsigned` only |
| `POST` | `/teams/{token}/est/selfsigned/simplereenroll` | **EST renewal** — `selfsigned` only |
| `GET` | `/teams/{token}/scep/adcs?operation=GetCACert` | SCEP: fetch ADCS CA certificate |
| `GET` | `/teams/{token}/scep/adcs?operation=GetCACaps` | SCEP: fetch capabilities |
| `POST` | `/teams/{token}/scep/adcs?operation=PKIOperation` | **SCEP enrollment** — `adcs` |
| `POST` | `/teams/{token}/ejbca/ejbca-rest-api/v1/certificate/pkcs10enroll` | **EJBCA REST enrollment** — `ejbca` |
| `POST` | `/teams/{token}/dicom/backends/{backend}/stow` | Submit DICOM — Flow 2 scoring |
| `POST` | `/teams/{token}/scoring/dicomweb-to-dimse` | Claim Flow 3 score |
| `POST` | `/teams/{token}/acme/challenge` | Store HTTP-01 challenge for step-ca validation |
| `GET` | `/.well-known/acme-challenge/{acmeToken}` | Serves stored HTTP-01 key authorization (called by step-ca) |
| `POST` | `/teams/{token}/api/backends/acme/claim` | Claim ACME cert for scoring (submit cert PEM) |
| `ANY` | `/teams/{token}/acme/**` | ACME protocol (proxied to step-ca) |
| `ANY` | `/acme/acme/**` | Shared ACME protocol endpoint (no team token — used after directory fetch) |

---

## DIMSE infrastructure

| Service | Hostname | IP | Port | Notes |
|---|---|---|---|---|
| Orthanc plain DIMSE | `kryptonian-dimse.eastus.cloudapp.azure.com` | `20.119.67.236` | `4242` | AET: `KRYPTONIAN`. No TLS. C-MOVE source, C-STORE target. |
| DIMSE TLS proxy (mTLS) | `kryptonian-dimse.eastus.cloudapp.azure.com` | `20.119.67.236` | `4243` | Present EST-enrolled cert as client credential. |
| Orthanc DICOMweb | `kryptonian-dimse.eastus.cloudapp.azure.com` | `20.119.67.236` | `8042` | WADO-RS, STOW-RS, QIDO-RS at `/dicom-web/`. |
| Proxy server cert | `kryptonian-dimse.eastus.cloudapp.azure.com` | `20.119.67.236` | `8044` | `GET /server-cert` — download and trust before connecting to port 4243. |

> Both the hostname and IP resolve to the same host. Use the hostname where possible (TLS SNI); use the IP if your DICOM gateway requires a numeric address.

---

## UI Demo scoring

Three points are awarded by judges during a live demonstration of your gateway's device management UI. These are **not automated** — a judge observes your UI and assigns 0 or 1 point per criterion.

### Criterion 1 — Device registration (1 pt)

Your gateway UI must have an explicit device registration step. When a device that has **not** been registered attempts to enroll a certificate, the gateway must reject the request before any CSR reaches the CA.

**Full point awarded when:**
- The UI has a visible "Register Device" workflow (form, import, etc.)
- A device list view shows registered devices with identifier, status, and timestamp
- An enrollment attempt from an unregistered device is visibly rejected (status badge, log entry, error response)
- The CA backend receives no CSR for the rejected device

### Criterion 2 — Pending registration status (1 pt)

Devices should enter a **pending** state after registration, requiring an explicit approval step before certificate enrollment is permitted.

**Full point awarded when:**
- Newly registered devices show a `pending` (or equivalent) status in the UI
- Enrollment from a pending device is rejected with a reason distinguishable from "unregistered" (e.g., "awaiting approval" vs "device not found")
- An explicit approve action transitions the device to `active` and enrollment then succeeds

### Criterion 3 — Device removal blocks renewal (1 pt)

Removing a device from the registry must prevent that device from obtaining a new or renewed certificate through any CA backend.

**Full point awarded when:**
- The UI has a visible "Remove" (or deactivate/revoke trust) action on registered devices
- After removal, an EST renewal attempt from that device is rejected at the gateway — no CSR reaches the CA
- The gateway enforces this across all configured CA backends (selfsigned, ADCS, EJBCA, ACME)
