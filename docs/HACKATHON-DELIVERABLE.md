# AAPM RT-SEC Hackathon 2026 — Scoring Guide

**Event:** AAPM Annual Meeting · Trento, Italy · 2026

---

## What You Are Building

A **Certificate Enrollment Gateway** that secures medical device communication over DICOM — the protocol that carries imaging data between scanners, PACS systems, and treatment delivery devices.

The problem: legacy DICOM (DIMSE) has no built-in authentication. Anyone on the network can impersonate a device. The solution: a gateway that enforces certificate-based identity using standard PKI (EST protocol), bridging legacy DIMSE to modern DICOMweb over mTLS.

> See the threat landscape diagram on the [hackathon landing page](https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/) for the full picture.

---

## The Scoring Infrastructure

A shared **CA Harness** is provided at `https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io`. It gives every team a private, isolated set of four certificate authority backends. Your gateway enrolls device certificates through these backends using EST, and the harness verifies your work in real time.

**Before you start:** register your team to get a private token:

```bash
curl -s -X POST https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/api/teams/register \
  -H "Content-Type: application/json" \
  -d '{"teamName": "Team Name Here"}'
# → {"token": "your-private-guid", "teamName": "..."}
```

All subsequent API calls use `{token}` in the URL path. Your token is private — the leaderboard only shows your team name.

📥 **[Download DICOM test files (28 MB)](https://stkryptonianfiles.blob.core.windows.net/downloads/dicom-examples.zip)** — 138 CT instances for your submissions.

**Full API reference:** [/api/help](https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/api/help)

**Live leaderboard:** [/scoreboard](https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/scoreboard)

---

## CA Backends

Your gateway must support all four CA backends. Each is a separate score column on the leaderboard.

| Backend | What it emulates | Enrollment protocol |
|---|---|---|
| `selfsigned` | Generic self-signed CA | EST — `simpleenroll` |
| `adcs` | Microsoft AD Certificate Services | SCEP — `PKIOperation` |
| `ejbca` | EJBCA Enterprise PKI | EJBCA REST — `pkcs10enroll` |
| `acme` | ACME (step-ca) | ACME RFC 8555 — HTTP-01 challenge relay |

---

## The Three Scoring Flows

Each flow tests a different capability of your gateway. Flows are independent — you can earn partial credit.

---

### Flow 1 — DIMSE mTLS C-STORE *(per CA backend, 4 points max)*

**What it proves:** Your gateway can enroll a device via EST and the resulting certificate provides authenticated access to a DICOM endpoint.

**How it works:**

1. Your gateway enrolls a device certificate using the protocol for the target backend:

   | Backend | Endpoint | Protocol |
   |---|---|---|
   | `selfsigned` | `POST /teams/{token}/est/selfsigned/simpleenroll` | EST |
   | `adcs` | `POST /teams/{token}/scep/adcs?operation=PKIOperation` | SCEP |
   | `ejbca` | `POST /teams/{token}/ejbca/ejbca-rest-api/v1/certificate/pkcs10enroll` | EJBCA REST |
   | `acme` | ACME RFC 8555 via `/teams/{token}/acme/directory` | ACME + HTTP-01 relay |

   Each backend embeds a unique OID in the issued certificate proving it came through the gateway enrollment path (not the raw `/issue` JSON API).

2. Your gateway/device connects to the **DIMSE TLS proxy** at `kryptonian-dimse.eastus.cloudapp.azure.com:4243` using that certificate as the mTLS client credential.
3. Perform a C-STORE of any DICOM file from the test set.
4. The proxy validates the certificate against the harness CAs, identifies your team, and automatically updates the scoreboard with 🔒.

   **Exception — ACME backend:** ACME certificates are issued by step-ca's CA (not the harness's in-memory CA), so they cannot be validated by the DIMSE proxy. For the ACME backend, the score is earned by claiming the cert: `POST /teams/{token}/api/backends/acme/claim` with the cert PEM.

**Trust the proxy server cert first:**
```bash
curl http://kryptonian-dimse.eastus.cloudapp.azure.com:8044/server-cert -o dimse-proxy.pem
# Add dimse-proxy.pem to your DICOM client's trust store
```

**Scored:** Once per CA backend. Four backends = four 🔒 badges maximum.

---

### Flow 2 — C-MOVE → DICOMWeb *(1 point)*

**What it proves:** Your gateway can bridge inbound DIMSE C-MOVE to outbound DICOMweb STOW-RS.

**How it works:**

1. After completing Flow 1 for any backend, that DICOM instance now lives in the Orthanc PACS at `kryptonian-dimse.eastus.cloudapp.azure.com:4242` (plain DIMSE, no TLS).
2. Your gateway/adapter sends a C-MOVE request to Orthanc (AET: `KRYPTONIAN`) asking it to push that study to your SCP.
3. Orthanc sends the DICOM file to your SCP via plain C-STORE.
4. Your gateway/adapter receives the file and submits it to the harness via DICOMweb:
   ```
   POST /teams/{token}/dicom/backends/{backend}/stow
   X-Device-Certificate: <certificateDerBase64 from your enrollment>
   X-Transfer-Mode: cmove
   Content-Type: application/dicom
   Body: <DICOM file>
   ```
5. The harness records the submission and updates the leaderboard with ⇌.

**Note:** The certificate used here must be EST-enrolled (has the OID extension) to earn gateway credit. Certificates from the `/issue` JSON API do not count.

---

### Flow 3 — DICOMWeb Retrieve → DIMSE C-STORE *(1 point)*

**What it proves:** Your gateway can bridge inbound DICOMweb WADO-RS retrieve to outbound DIMSE C-STORE.

**How it works:**

1. Your gateway/adapter performs a WADO-RS retrieve from Orthanc's DICOMweb endpoint:
   ```
   GET http://kryptonian-dimse.eastus.cloudapp.azure.com:8042/dicom-web/studies/{studyUID}/...
   ```
2. Your gateway receives the DICOM file over HTTP.
3. Your gateway performs a DIMSE C-STORE (plain, no TLS) back to Orthanc at port 4242.
4. After the C-STORE, call the harness to claim the score:
   ```
   POST /teams/{token}/scoring/dicomweb-to-dimse
   Content-Type: application/json
   {"sopInstanceUid": "<uid of the instance you stored>"}
   ```
5. The harness queries Orthanc to verify the instance is present and awards the point.

---

## What Your Gateway Must Demonstrate

For the **full score** and the live demo, judges want to see:

1. **Device registration UI** — an admin registers a new device (generates keypair + CSR inside the gateway)
2. **Gateway enrollment** — the gateway calls the appropriate harness endpoint for each backend:
   - `selfsigned`: EST `simpleenroll`
   - `adcs`: SCEP `PKIOperation`
   - `ejbca`: EJBCA REST `pkcs10enroll`
   - `acme`: ACME RFC 8555 with HTTP-01 challenge relay (see ACME section in the API guide)
3. **Certificate-gated DIMSE** — for `selfsigned`, `adcs`, and `ejbca`: the enrolled cert is used to C-STORE to port 4243 (Flow 1). For `acme`: claim the cert via `/api/backends/acme/claim`.
4. **DIMSE→DICOMweb bridge** — the gateway triggers a C-MOVE and converts the result to STOW-RS (Flow 2)
5. **DICOMweb→DIMSE bridge** — the gateway does a WADO-RS retrieve and converts to C-STORE (Flow 3)
6. **Device removal UI** — admin removes a device; subsequent renewal is rejected by the gateway (the gateway must enforce this, not just the harness)

**Repeat enrollment for all four CA backends** to claim a full sweep.

---

## Certificate Rules

- Certificates from `POST /teams/{token}/api/backends/{backend}/issue` (the JSON API) **do not earn gateway credit**. They are for testing only.
- Gateway-credit certificates must come through the per-backend enrollment protocol:
  - `selfsigned` → EST `simpleenroll` → OID `1.3.6.1.4.1.99999.1`
  - `adcs` → SCEP `PKIOperation` → OID `1.3.6.1.4.1.99999.2`
  - `ejbca` → EJBCA REST `pkcs10enroll` → OID `1.3.6.1.4.1.99999.3`
  - `acme` → ACME RFC 8555 → claim cert via `/api/backends/acme/claim`
- The DIMSE proxy at port 4243 validates `selfsigned`, `adcs`, and `ejbca` certs against the harness CAs and awards 🔒. The ACME backend is scored by cert claim only.

---

## Scoring Summary

| Achievement | Points | How |
|---|---|---|
| DIMSE mTLS C-STORE — selfsigned | 1 | Flow 1 via EST → port 4243 |
| DIMSE mTLS C-STORE — adcs | 1 | Flow 1 via SCEP → port 4243 |
| DIMSE mTLS C-STORE — ejbca | 1 | Flow 1 via EJBCA REST → port 4243 |
| ACME backend complete — acme | 1 | ACME RFC 8555 → `/api/backends/acme/claim` |
| C-MOVE → DICOMweb | 1 | Flow 2 |
| DICOMweb → DIMSE | 1 | Flow 3 |
| **Maximum** | **6** | |

Tiebreaker: earliest timestamp on first completed flow.

---

## Infrastructure Reference

| Service | Address | Purpose |
|---|---|---|
| CA Harness API | `https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io` | Certificate issuance, scoring |
| Orthanc DIMSE (plain) | `kryptonian-dimse.eastus.cloudapp.azure.com:4242` | C-MOVE source, C-STORE target (no TLS) |
| DIMSE TLS Proxy | `kryptonian-dimse.eastus.cloudapp.azure.com:4243` | mTLS C-STORE with your EST cert |
| Orthanc DICOMweb | `http://kryptonian-dimse.eastus.cloudapp.azure.com:8042` | WADO-RS retrieve |
| Proxy cert download | `http://kryptonian-dimse.eastus.cloudapp.azure.com:8044/server-cert` | Trust this PEM for port 4243 |
| Leaderboard | `/scoreboard` | Live team standings |
| API Guide | `/api/help` | Full endpoint reference |
