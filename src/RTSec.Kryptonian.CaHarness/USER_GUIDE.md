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

| Value | CA type | EST enroll | ACME |
|---|---|---|---|
| `selfsigned` | Self-signed CA | ✓ | — |
| `adcs` | Microsoft AD CS | ✓ | — |
| `ejbca` | EJBCA | ✓ | — |
| `acme` | ACME via step-ca | — | ✓ (use `DirectoryUrl`) |

**EST-enrollable backends:** `selfsigned`, `adcs`, `ejbca`  
**ACME backend:** configure your gateway's `DirectoryUrl` to `/teams/{token}/acme/directory` — do not use `acme` in EST paths.

---

## Scoring overview

Three flows score points on the leaderboard. **All three require your gateway to use the EST enrollment path** — certificates from the `/issue` JSON API do not earn gateway credit.

| Flow | What it tests | Points |
|---|---|---|
| Flow 1 — DIMSE mTLS C-STORE | EST-enrolled cert → mTLS C-STORE to DIMSE proxy | 1 per CA backend (4 max) |
| Flow 2 — C-MOVE → DICOMWeb | C-MOVE from Orthanc → your SCP → STOW-RS to harness | 1 |
| Flow 3 — DICOMWeb → DIMSE | WADO-RS retrieve from Orthanc → C-STORE back to Orthanc | 1 |

---

## EST enrollment (required for scoring)

To earn gateway credit your device certificates **must** be issued via the EST endpoints. These embed a private OID (`1.3.6.1.4.1.99999.1`) that is verified by both the DIMSE proxy and the STOW-RS endpoint. Certificates from `/issue` never carry this OID.

**Enroll a new device:**
```bash
CSR=$(openssl req -newkey rsa:2048 -nodes -keyout device.key \
  -subj "/CN=my-device/O=Hospital/C=US" -outform DER 2>/dev/null | base64 -w 0)

curl -s -X POST https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{token}/est/{backend}/simpleenroll \
  -H "Content-Type: application/pkcs10" \
  -H "X-Device-Id: my-device-001" \
  --data "$CSR"
```

**Renew a device certificate:**
```bash
curl -s -X POST https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{token}/est/{backend}/simplereenroll \
  -H "Content-Type: application/pkcs10" \
  -H "X-Device-Id: my-device-001" \
  --data "$CSR"
```

Both return a standard `IssueResponse` JSON with `certificatePem` and `certificateDerBase64`.

`{backend}` can be `selfsigned`, `adcs`, or `ejbca` for EST enrollment. For ACME, point your gateway `DirectoryUrl` at the harness (see ACME section below).

---

## Flow 1 — DIMSE mTLS C-STORE

The DIMSE TLS proxy sits at **`kryptonian-dimse.eastus.cloudapp.azure.com:4243`**. It accepts DICOM C-STORE connections with mTLS. It validates your client certificate against all team CAs and automatically records the score.

**Step 1 — Trust the proxy server certificate:**
```bash
curl http://kryptonian-dimse.eastus.cloudapp.azure.com:8044/server-cert -o dimse-proxy.pem
# Add dimse-proxy.pem to your DICOM client trust store
```

**Step 2 — EST-enroll your device** (see EST section above).

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

`X-Device-Certificate` must be the `certificateDerBase64` from your EST enrollment (base64 DER — **not PEM**, which has newlines that are invalid in headers).

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

| Backend | What it emulates | EST / issue requirement |
|---|---|---|
| `selfsigned` | Generic self-signed CA | none |
| `adcs` | Microsoft AD CS | `templateName`: `DicomDeviceAuthentication` or `DicomBridgeMtls` |
| `ejbca` | EJBCA | `templateName`: `MedicalDeviceTLS` or `DicomWebBridgeMTLS` |
| `acme` | ACME (step-ca) | `DirectoryUrl` in gateway config |

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

Point your gateway's ACME `DirectoryUrl` at the harness using your token:

```
https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{token}/acme/directory
```

Do **not** point directly at step-ca — the harness will not log your team's activity.

```bash
# Check ACME activity
curl https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{token}/api/backends/acme/activity

# Check issued certs (must have at least one for ACME score)
curl https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{token}/api/backends/acme/issued
```

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
| `POST` | `/teams/{token}/est/{backend}/simpleenroll` | **EST enrollment** (earns gateway credit) |
| `POST` | `/teams/{token}/est/{backend}/simplereenroll` | **EST renewal** (earns gateway credit) |
| `POST` | `/teams/{token}/dicom/backends/{backend}/stow` | Submit DICOM — Flow 2 scoring |
| `POST` | `/teams/{token}/scoring/dicomweb-to-dimse` | Claim Flow 3 score |
| `ANY` | `/teams/{token}/acme/**` | ACME protocol (proxied to step-ca) |

---

## DIMSE infrastructure

| Service | Hostname | IP | Port | Notes |
|---|---|---|---|---|
| Orthanc plain DIMSE | `kryptonian-dimse.eastus.cloudapp.azure.com` | `20.119.67.236` | `4242` | AET: `KRYPTONIAN`. No TLS. C-MOVE source, C-STORE target. |
| DIMSE TLS proxy (mTLS) | `kryptonian-dimse.eastus.cloudapp.azure.com` | `20.119.67.236` | `4243` | Present EST-enrolled cert as client credential. |
| Orthanc DICOMweb | `kryptonian-dimse.eastus.cloudapp.azure.com` | `20.119.67.236` | `8042` | WADO-RS, STOW-RS, QIDO-RS at `/dicom-web/`. |
| Proxy server cert | `kryptonian-dimse.eastus.cloudapp.azure.com` | `20.119.67.236` | `8044` | `GET /server-cert` — download and trust before connecting to port 4243. |

> Both the hostname and IP resolve to the same host. Use the hostname where possible (TLS SNI); use the IP if your DICOM gateway requires a numeric address.
