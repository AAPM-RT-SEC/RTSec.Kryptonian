# Kryptonian Formal Engineering Specification — Interface Control Document

| Field | Value |
|---|---|
| Document ID | KSPEC-005 |
| Title | Interface Control Specification (EST, Admin REST, CA Connector, and Supporting Interfaces) |
| Version | 0.2 (Draft) |
| Status | Draft — pending review |
| Created | 2026-10-13 |
| Baseline commit | `682f996c8447afec13bc1fce5b7f59e6a06efa25` (branch `feat/windows-sandbox-installer`) |
| Repository | https://github.com/AAPM-RT-SEC/RTSec.Kryptonian |
| Product classification | Enterprise healthcare IT cybersecurity infrastructure (certificate enrollment gateway). **Not** a medical device; no clinical functions; no regulatory content. |
| Revision history | 0.1 — Initial draft generated from baseline commit inspection. |
| Source references | KSPEC-000 (conventions); `EstController.cs`, `CrlController.cs`, `AcmeChallengeController.cs`, all Admin controllers under `src/RTSec.Kryptonian.Api/Controllers/`; `ApiKeyAuthenticationHandler.cs`, `ConfiguredClientCertificateTrust.cs`, `CertificateForwardingGuard.cs`, `Program.cs`; `src/RTSec.Kryptonian.Domain/Interfaces/`; `src/RTSec.Kryptonian.Domain/ValueObjects/`; `src/RTSec.Kryptonian.Infrastructure/{Harness,Acme,Crypto,Data}/`; `src/RTSec.Kryptonian.Application/{DTOs,Services}/`; `OpenAPI.Admin.yaml`, `OpenAPI.Client.yaml`, `OpenAPI.ESTProfiles.yaml`, `OpenAPI.Schema.yaml`, `OpenAPI.StatusObserve.yaml`; `docs/API-GUIDE.md`, `docs/device-enrollment.md`, `docs/ca-backend-configuration.md`, `docs/user-management.md`, `docs/IMPLEMENTATION-TRACKER.md` |
| Author | ______________ (name–date–signature) |
| Reviewer | ______________ (name–date–signature) |
| Approver | ______________ (name–date–signature) |
| Reconciliation revision | 0.2 - 2026-10-13 - source/document audit and publication controls (KSPEC-016) |

**Requirement references:** KSPEC-003 is the canonical register. Area-level interface references are bound to concrete requirements in KSPEC-009 §§4 and 7. `SR-EST`, `SR-ADM`, and `SR-CAI` are legacy area aliases for `SR-F-EST`, `SR-F-ADM`, and `SR-F-CAI`; they allocate no IDs. Provenance and AS-IS / TO-BE / PROPOSED classifications follow KSPEC-000.

---

## 1. Scope and interface inventory

This ICD specifies every externally and internally observable interface of the
Kryptonian gateway at the baseline commit:

| ICD item | Interface group | Sections |
|---|---|---|
| ICD-EST.001–ICD-EST.006 | EST protocol (RFC 7030) device-facing endpoints | §3–§9 |
| ICD-ADM.001–ICD-ADM.012 | Administrative REST APIs (`/api/*`) | §10–§11 |
| ICD-ERR.001 | Error model and error codes | §12 |
| ICD-PCR.001–ICD-PCR.003 | Certificate, CSR, and CRL payload formats | §13 |
| ICD-CAI.001–ICD-CAI.003 | Internal CA connector contract and per-connector capabilities | §14 |
| ICD-DAT.001 | Database integration (EF Core / SchemaUpgrades) | §15 |
| ICD-CFG.001–ICD-CFG.002 | Configuration interfaces (appsettings, environment, secrets) | §16 |
| ICD-EXT.001–ICD-EXT.004 | External system dependencies (SMTP, ACME, ADCS/NDES, EJBCA, harness) | §17 |
| ICD-VER.001–ICD-VER.003 | Versioning and interoperability policy | §18 |

Each section below separates **implemented endpoint behavior (AS-IS)** from
**approved interface contracts (TO-BE/PROPOSED)**. Nothing in this document is
normative until the corresponding contract is approved; AS-IS descriptions are
evidence of prototype behavior only (KSPEC-000 §4, §13).

---

## 2. Interface surface overview

AS-IS (CODE-OBSERVED): the gateway exposes two distinct API surfaces plus two
protocol-support surfaces:

| Surface | Base path | Transport | Authentication | Audience |
|---|---|---|---|---|
| EST protocol | `/.well-known/est[/{label}]` | HTTPS (port 8443 by config default) | TLS client certificate (re-enroll); HTTP Basic one-time activation bootstrap or legacy activation headers (initial enroll) | Medical devices / EST clients |
| Admin REST API | `/api/*` | HTTPS according to configured Kestrel listener; AdminPort key alone does not bind a listener | JWT Bearer or `X-API-Key` | Administrators, Blazor/React UI, automation |
| CRL distribution | `/api/crl/{issuerFingerprint}.crl` | HTTPS | None (public, `AllowAnonymous`) | Any peer performing revocation checks |
| ACME HTTP-01 challenges | `/.well-known/acme-challenge/{token}` | HTTP(S) | None (anonymous by ACME design) | ACME servers |

Controller routes are ASP.NET Core attribute routes, case-insensitive for path
segments. Swagger UI is exposed in Development mode only (`/swagger`) with an
`ApiKey` security scheme declared (CODE-OBSERVED, `Program.cs`).

---

## 3. EST endpoints (ICD-EST.001) — AS-IS

**Provenance:** CODE-OBSERVED (source inspection of `EstController.cs` at baseline).

| Method | Path | Behavior |
|---|---|---|
| GET | `/.well-known/est/cacerts` | Returns the CA certificate chain of the EST profile resolved from `Request.Host.Host` and path prefix `/.well-known/est`. |
| GET | `/.well-known/est/{label}/cacerts` | Same, with path prefix `/.well-known/est/{label}`. Unknown labels are **rejected with 404**; they never fall back to the default profile. |
| POST | `/.well-known/est/simpleenroll` | Initial enrollment. Accepts a PKCS#10 CSR; returns PKCS#7 cert chain; supports pending (202) responses. |
| POST | `/.well-known/est/{label}/simpleenroll` | Same, profile-scoped by label. |
| POST | `/.well-known/est/simplereenroll` | Renewal. Requires a native TLS client certificate; returns PKCS#7 cert chain; supports pending (202) responses. |
| POST | `/.well-known/est/{label}/simplereenroll` | Same, profile-scoped by label. |

Profile resolution rules (AS-IS, CODE-OBSERVED, `ResolveProfileIdAsync`):

1. The profile is resolved by the pair (hostname of `Request.Host`, effective path
   prefix). Unlabeled requests resolve only against `/.well-known/est`; labeled
   requests only against `/.well-known/est/{label}`.
2. A disabled or unmatched profile yields `404 {"error": "EST profile not found"}`.
3. Unknown labels are rejected rather than silently falling back to the default
   profile (prevents mistyped/revoked labels enrolling against the wrong CA).

A plain-HTTP request to any `/.well-known/est` path is answered with **426 Upgrade
Required** and a JSON error, unless `Kryptonian:Est:AllowPlainHttp=true` is set
(CODE-OBSERVED, `Program.cs` HTTPS-enforcement middleware).

---

## 4. EST authentication methods (ICD-EST.002) — AS-IS

**Provenance:** CODE-OBSERVED.

### 4.1 Native TLS client certificate (re-enroll, and optionally enroll)

- Kestrel is configured with `ClientCertificateMode.AllowCertificate` on the
  EST listener; the presented certificate is available as
  `HttpContext.Connection.ClientCertificate`.
- `simplereenroll` **requires** a client certificate: absence yields
  `401 {"error": "Client certificate required"}` (CODE-OBSERVED).
- `simpleenroll` requires a client certificate only when the profile's
  `requireClientCertificate` flag is true (`401` if absent). When a certificate is
  presented on either endpoint it is always validated regardless of the flag.
- Certificate validation (both endpoints): expiry/not-yet-valid checks; then, if
  the profile enables `validateClientCertificateChain`, a custom-root-trust chain
  build against the profile's `trustedClientCaThumbprints` (SHA-256 hash or
  SHA-1 thumbprint accepted), application policy OID clientAuth
  (`1.3.6.1.5.5.7.3.2`), online revocation, 5-second URL retrieval timeout.
  Failures yield `403`. A profile requiring chain validation with no trusted CAs
  configured fails closed (`403`, CODE-OBSERVED).
- TLS-listener-level validation is additionally performed by
  `ConfiguredClientCertificateTrust` when `Kryptonian:Tls:ClientCaFiles` is
  configured: the certificate must be a leaf (not a CA) with clientAuth EKU, must
  chain to the configured CA files under `CustomRootTrust`, revocation checked
  **online** (default), 5-second timeout (CODE-OBSERVED).

### 4.2 HTTP Basic bootstrap (one-time activation credentials)

Standard HTTP Basic is the bootstrap method for initial enrollment
(AS-IS, CODE-OBSERVED; introduced by PR #42 "Standard Basic EST bootstrap; atomic
one-time activation" per `docs/IMPLEMENTATION-TRACKER.md`):

- Header format: `Authorization: Basic base64(<deviceIdGuid>:<activationCode>)`
  where `<deviceIdGuid>` is the device record identifier in Guid "D" form and the
  activation code is the one-time code generated by the admin workflow
  (`POST /api/devices/{id}/activation-code`).
- The username must parse exactly as a Guid; otherwise
  `401 {"error": "Invalid Basic authentication credentials"}` with
  `WWW-Authenticate: Basic realm="EST", charset="UTF-8"` (CODE-OBSERVED).
- **TLS is mandatory for Basic**: if the request is not HTTPS while carrying Basic
  credentials, the gateway returns `403 {"error": "Basic authentication requires TLS"}`
  (CODE-OBSERVED).
- The activation code is validated by the enrollment orchestrator: it must exist,
  be unexpired, be unused, and belong to the device record; it is consumed
  atomically after the first certificate is issued (CODE-OBSERVED;
  DOCUMENTED in `docs/device-enrollment.md`; executed evidence in
  `docs/IMPLEMENTATION-TRACKER.md` — one-time activation replay rejected).

### 4.3 Legacy activation headers (compat only — NOT an interoperability requirement)

AS-IS compat only (CODE-OBSERVED; explicitly bounded by
`docs/IMPLEMENTATION-TRACKER.md`):

- `X-Activation-Code` — one-time activation code (when no Basic credentials are
  presented).
- `X-Device-Manufacturer`, `X-Device-Model`, `X-Device-Serial-Number` — activation
  metadata carried as request headers.
- `X-Device-Id` — accepted on requests but **untrusted**: logged for audit
  correlation only, never used for authentication or authorization (CODE-OBSERVED,
  `GetDeviceIdentifier` returns `null` when only the header is present).

Per `docs/IMPLEMENTATION-TRACKER.md` (DOCUMENTED): the sample device client retains
these legacy headers for compatibility; they are not mandatory for other clients
and **shall not become a new interoperability requirement** (KSPEC-000 §3, §4).

### 4.4 Not-authenticated-enrollment caveat

The AS-IS API guide states initial enrollment "may allow unauthenticated requests
depending on profile configuration" (`docs/API-GUIDE.md`, DOCUMENTED). Source
inspection shows an activation credential (Basic or `X-Activation-Code`) is in
practice required to complete enrollment issuance (CODE-OBSERVED,
`EnrollmentOrchestrator.EnrollAsync`). Whether a no-credential enrollment path can
succeed on a profile with `requireClientCertificate=false` is **UNKNOWN** from
inspected evidence and is recorded as an open question for KSPEC-003/KSPEC-010.

---

## 5. EST request/response schemas and content types (ICD-EST.003) — AS-IS

**Provenance:** CODE-OBSERVED (`EstController.cs`, `PkcsService.cs`).

Request:

- Content-Type: `application/pkcs10` or `text/plain` (declared via `[Consumes]`;
  both are accepted). The body carries a PKCS#10 CSR, base64-encoded when
  `Content-Transfer-Encoding: base64` is present, otherwise raw bytes that the
  parser accepts as DER, base64, or PEM (`-----BEGIN CERTIFICATE REQUEST-----` /
  legacy `-----BEGIN NEW CERTIFICATE REQUEST-----`, CODE-OBSERVED,
  `PkcsService.ParsePkcs10`).
- Request body size is limited to 65,536 bytes (`RequestSizeLimit` and decoder
  `maxSize`); exceeding it yields `413 {"error": "Request body too large"}`.
- Header `Content-Transfer-Encoding: base64` is honored per RFC 7030 §3.5
  (base64 transfer of the CSR).

Response (success):

- HTTP 200; Content-Type: `application/pkcs7-mime; smime-type=certs-only`;
  `Content-Transfer-Encoding: base64`; body is base64 PKCS#7 degenerate
  SignedData ("certs-only") containing the issued certificate and chain
  (CODE-OBSERVED).
- The task-relevant media types `application/pkcs10-mime` and
  `application/pkix-cert` are **not used anywhere in the baseline implementation**;
  the implemented content types are `application/pkcs10` (requests) and
  `application/pkcs7-mime` (responses), with `application/pkix-crl` used only by
  the CRL endpoint (§13). This is recorded as an AS-IS observation; RFC 7030
  itself specifies `application/pkcs10` for CSR submission, so the implemented
  choice matches the RFC media type; this is not a whole-protocol conformity claim (RFC 7030 §§4.2.1 and 4.2.3: https://www.rfc-editor.org/rfc/rfc7030.html).

Response (error) — see §12 for the full model:

- Content-Type `application/json`, body `{"error": "<message>"}` or
  `{"error": "<message>", "retryAfter": <seconds>}` for pending responses.

Consolidated EST path set — **TO-BE contract fragment (PROPOSED, OpenAPI 3.1,
contract-first)**. The repository OpenAPI files do not yet contain the EST paths;
the fragment below consolidates the AS-IS behavior for review as the approved
EST contract. This is PROPOSED and not normative until approved:

```yaml
openapi: 3.1.0
info: { title: Kryptonian EST (RFC 7030), version: 0.1.0-draft }
paths:
  /.well-known/est/{label}/cacerts:
    get:
      operationId: estGetCaCerts
      parameters:
        - { name: label, in: path, required: true, schema: { type: string } }
      responses:
        '200':
          description: CA certificate chain, PKCS#7 certs-only, base64
          headers:
            Content-Transfer-Encoding: { schema: { type: string, const: base64 } }
          content:
            application/pkcs7-mime:
              schema: { type: string, contentEncoding: base64 }
        '404': { description: EST profile not found (JSON error body) }
  /.well-known/est/{label}/simpleenroll:
    post:
      operationId: estSimpleEnroll
      parameters:
        - { name: label, in: path, required: true, schema: { type: string } }
      requestBody:
        required: true
        content:
          application/pkcs10:
            schema: { type: string, contentEncoding: base64 }
      responses:
        '200':
          description: Issued certificate chain (PKCS#7 certs-only, base64)
          content:
            application/pkcs7-mime:
              schema: { type: string, contentEncoding: base64 }
        '202':
          description: Issuance pending; retry per Retry-After
          headers:
            Retry-After: { schema: { type: integer, description: seconds } }
          content:
            application/json:
              schema:
                type: object
                properties:
                  error: { type: string, const: "Enrollment pending" }
                  retryAfter: { type: integer }
        '400': { description: Invalid CSR or request }
        '401': { description: Client certificate or activation credential missing/invalid }
        '403': { description: Certificate validation failed / Basic without TLS }
        '404': { description: EST profile not found }
        '413': { description: CSR exceeds 64 KiB }
        '429': { description: IP rate limit exceeded }
        '500': { description: Internal error }
```

---

## 6. EST protocol status handling: pending responses and Retry-After (ICD-EST.004) — AS-IS

**Provenance:** CODE-OBSERVED (`EnrollmentResult`, `EstController.HandleEnrollmentResult`).

| Condition | Status | Headers | Body |
|---|---|---|---|
| Certificate issued | 200 | `Content-Transfer-Encoding: base64` | PKCS#7 certs-only (base64) |
| Async issuance pending | 202 | `Retry-After: <seconds>` when the result carries `RetryAfterSeconds` | `{"error": "Enrollment pending", "retryAfter": <seconds>}` (JSON) |
| Invalid CSR / argument | 400 | — | `{"error": ...}` |
| Missing/invalid credential or client certificate | 401 | `WWW-Authenticate: Basic realm="EST", charset="UTF-8"` for Basic failures | `{"error": ...}` |
| Validation/policy rejection | 403 | — | `{"error": <reason>}` |
| Unknown/disabled profile | 404 | — | `{"error": "EST profile not found"}` |
| Body over 64 KiB | 413 | — | `{"error": "Request body too large"}` |
| IP rate limit exceeded | 429 | per AspNetCoreRateLimit | rate-limit response |
| Unexpected failure | 500 | — | `{"error": "Internal server error"}` |

Pending behavior notes (AS-IS):

- The 202 body is a JSON error object, **not** a PKCS#7 body; clients retry the
  same `simpleenroll`/`simplereenroll` request after the indicated delay
  (DOCUMENTED in `docs/API-GUIDE.md`: "The client should wait the specified
  number of seconds before retrying the request").
- `Retry-After` is emitted as a delay-seconds value only (never an HTTP-date).
- Executed evidence of pending/renewal behavior exists in
  `docs/IMPLEMENTATION-TRACKER.md` (PR #44) but the specific trigger conditions
  for pending responses (which CA backend states produce `IsPending`) were not
  exhaustively inspected and are **UNKNOWN** in detail.

TO-BE/PROPOSED: the approved contract should require that every pending-capable
CA backend produce a deterministic `Retry-After` value and that clients treat
202 as retryable-with-backoff, subject to the consumed-bootstrap limitation (GAP-039) (SR-EST, SR-F-INT). Not yet approved.

---

## 7. TLS requirements for EST (ICD-EST.005) — AS-IS

**Provenance:** CODE-OBSERVED (`Program.cs`, `ConfiguredClientCertificateTrust.cs`,
`CertificateForwardingGuard.cs`).

- EST requires HTTPS by default; plain HTTP is refused with `426 Upgrade Required`
  unless `Kryptonian:Est:AllowPlainHttp=true` (development escape hatch).
- HTTP Basic credentials are additionally rejected over plain HTTP with `403`
  (defense in depth, CODE-OBSERVED).
- TLS configuration keys: `Kryptonian:Tls:RequireTls12` (default true in
  `appsettings.json`), `Kryptonian:Tls:EstPort` (default 8443),
  `Kryptonian:Tls:AdminPort` (default 8080).
- Client certificate mode is `AllowCertificate` (certificate optional at TLS
  layer; enforcement happens per endpoint/profile as in §4).
- Optional strict listener validation via `Kryptonian:Tls:ClientCaFiles`: CA PEM
  files loaded at startup; a file set that yields no CA certificate fails startup
  with `InvalidOperationException` (fail-closed, CODE-OBSERVED).
- Reverse proxy support: certificate forwarding uses the Envoy
  `X-Forwarded-Client-Cert` header format (`Hash=...;Cert="<URL-encoded PEM>"`).
  `CertificateForwardingGuard` rejects (`403`) any request carrying this header
  from a non-configured proxy (`Kryptonian:CertificateForwarding:KnownProxies`)
  or when a native client certificate is also present, and rejects (`400`)
  multiple forwarding headers (CODE-OBSERVED).

---

## 8. EST rate limiting (ICD-EST.006) — AS-IS

CODE-OBSERVED (`appsettings.json`, AspNetCoreRateLimit):

| Endpoint rule | Period | Limit |
|---|---|---|
| `*:/.well-known/est/*` | 1 minute | 100 |
| `POST:/.well-known/est/*/simpleenroll` | 1 minute | 30 |
| `POST:/.well-known/est/*/simplereenroll` | 1 minute | 30 |

The middleware is configured to return HTTP 429 for blocked requests; effective route matching and load behavior are unverified in this review (VT-055). Real client IP is taken from
`X-Forwarded-For` (`RealIpHeader`), also used by the EST controller for source-address logging. Consistent use does not authenticate this header; spoof-resistant attribution requires trusted-forwarder enforcement (GAP-040).

---

## 9. RFC 7030 conformance points and deviations — AS-IS assessment

**Provenance:** CODE-OBSERVED for gateway behavior; conformance judgment is
INFERRED and shall not be presented as a standards-conformity claim (KSPEC-000 §5,
§13; no adopted EST profile is claimed in `docs/IMPLEMENTATION-TRACKER.md`).

| RFC 7030 point | Baseline status | Classification |
|---|---|---|
| `/cacerts` returns PKCS#7 certs-only, base64 CTE | Implemented (`application/pkcs7-mime; smime-type=certs-only`) | Implemented (source inspection; conformity untested) |
| `/simpleenroll` accepts `application/pkcs10` (base64 CTE), returns PKCS#7 | Implemented; also accepts `text/plain` (lenient extension) | Implemented; text/plain is an additional accepted media type |
| `/simplereenroll` authenticates with existing credential | Implemented via native TLS client certificate | Implemented (source inspection; conformity untested) |
| Client certificate identity binding on re-enroll | Implemented; orchestrator binds to approved identity, preserves it on renewal (executed evidence, PRs #38/#44) | Implemented (historical lifecycle evidence) |
| HTTP Basic bootstrap per RFC 7030 §3.2.3 | Implemented (Guid-as-username one-time activation scheme) | Site-specific credential provisioning, not a Basic wire-format deviation |
| `WWW-Authenticate` challenge on Basic failures | Implemented | Implemented (source inspection; conformity untested) |
| `/csrattrs` (attribute discovery) | **Not implemented** | Missing |
| `/serverkeygen` (server-generated keys / KRA) | **Not implemented** | Missing |
| `/fullcmc` (full CMS enrollment) | **Not implemented** | Missing |
| Label-based virtual EST server (`{label}` segments) | Implemented; unknown labels rejected (stricter than RFC's flexible-label guidance) | Implemented (strict profile lookup; interoperability untested) |
| Pending / 202 + `Retry-After` | Implemented status/header branch; backend pending workflow unverified | RFC-defined pending response, not a proprietary extension |
| Plain-HTTP refusal (426) | Implemented | Extension (security-positive) |

**Deviations/limitations summary (AS-IS):** no `/csrattrs`, no `/serverkeygen`,
no `/fullcmc`; Basic bootstrap username is the gateway device-record Guid rather
than a pre-existing user identity; `text/plain` CSR bodies accepted leniently;
legacy `X-*` activation/device headers retained for the bundled sample client
only and must not become an interoperability requirement.

---

## 10. Administrative REST APIs (ICD-ADM.001–ICD-ADM.012) — AS-IS

**Provenance:** CODE-OBSERVED (controller source inspection). All `/api/*`
controllers produce `application/json`. Authentication is dual-scheme: JWT Bearer
(`Authorization: Bearer`, issuer `kryptonian`, audience `kryptonian-ui`, 8-hour
lifetime, 5-minute clock skew) or API key (`X-API-Key` header — static config
keys acting as SystemAdmin, or DB-stored user keys with `kry_` prefix, SHA-256
hashed, expiry and user-active checks). PolicyScheme selects JWT when an
`Authorization` header is present, otherwise the API-key scheme
(CODE-OBSERVED, `Program.cs`, `ApiKeyAuthenticationHandler.cs`).

Authorization policies (CODE-OBSERVED): default policy = authenticated user;
`DeviceAdmin` = role DeviceAdmin or SystemAdmin; `SystemAdmin` = role SystemAdmin.
Roles per `docs/user-management.md` (DOCUMENTED): Standard, DeviceAdmin,
SystemAdmin.

### ICD-ADM.001 Devices (`api/devices`) — CODE-OBSERVED

| Method | Path | Authorization | Success | Notes |
|---|---|---|---|---|
| GET | `/api/devices` | authenticated | 200 `DeviceDto[]` | |
| POST | `/api/devices` | DeviceAdmin | 201 `DeviceDto` | 400 on invalid input |
| POST | `/api/device-requests` | **anonymous** | 202 `DeviceApprovalRequestResponseDto` | Device self-request for approval; 400 on invalid |
| GET | `/api/devices/{id}` | authenticated | 200 `DeviceDto` / 404 | Non-Guid id → 404 |
| POST | `/api/devices/{id}/approve` | DeviceAdmin | 200 `DeviceDto` / 404 | |
| POST | `/api/devices/{id}/activation-code` | DeviceAdmin | 200 `DeviceActivationCodeDto` / 400 / 404 | Body `{"validForMinutes": int?}` |
| POST | `/api/devices/{id}/activation-code/reactivate` | DeviceAdmin | 200 `DeviceActivationCodeDto` / 400 / 404 | |
| POST | `/api/devices/{id}/remove` | DeviceAdmin | 200 `DeviceDto` / 404 | Soft removal; removed devices cannot enroll/renew (DOCUMENTED) |
| DELETE | `/api/devices/{id}` | DeviceAdmin | 204 / 400 / 404 | Permanent purge; may be rejected (400) |
| GET | `/api/devices/{id}/certificates` | authenticated | 200 `CertificateDto[]` / 404 | |
| GET | `/api/devices/{id}/events` | DeviceAdmin | 200 `DeviceEventDto[]` / 404 | Lifecycle + enrollment events |
| GET | `/api/devices/certificates` | authenticated | 200 `CertificateDto[]` | All certificates, newest first (avoids N+1 pool exhaustion, CODE-OBSERVED) |
| POST | `/api/devices/{id}/demo-enroll` | DeviceAdmin | 200 `DemoEnrollResponseDto` / 400 / 404 | Admin-side demo enrollment via active profile |

`DeviceActivationCodeDto` includes the raw `activationCode` and `qrPayload`
(show-once delivery; the gateway stores activation codes hashed,
DOCUMENTED).

### ICD-ADM.002 CA backends (`api/cas`) — CODE-OBSERVED

| Method | Path | Authorization | Success | Notes |
|---|---|---|---|---|
| GET | `/api/cas` | authenticated | 200 `CaBackendDto[]` | |
| GET | `/api/cas/active` | authenticated | 200 `CaBackendDto` / 404 | |
| POST | `/api/cas` | SystemAdmin | 201 `CaBackendDto` | 400 on invalid |
| GET | `/api/cas/{id}` | authenticated | 200 `CaBackendDto` / 404 | |
| PUT | `/api/cas/{id}` | SystemAdmin | 200 `CaBackendDto` / 400 / 404 | |
| DELETE | `/api/cas/{id}` | SystemAdmin | 204 / 400 / 404 | 400 when backend in use (InvalidOperationException) |
| POST | `/api/cas/{id}/test` | authenticated | 200 `{"success": bool}` / 404 | No role policy beyond authentication (AS-IS observation) |
| POST | `/api/cas/{id}/activate` | SystemAdmin | 200 `CaBackendDto` / 400 / 404 | Single active backend |
| POST | `/api/cas/generate-selfsigned` | SystemAdmin | 200 `{pfxPath, thumbprint, notAfter}` | 400 invalid/permissions; 409 existing file; 500 crypto failure (matches `OpenAPI.Admin.yaml`) |

### ICD-ADM.003 EST profiles (`api/est-profiles`) — CODE-OBSERVED

| Method | Path | Authorization | Success | Notes |
|---|---|---|---|---|
| GET | `/api/est-profiles` | authenticated | 200 `EstProfileDto[]` | |
| POST | `/api/est-profiles` | SystemAdmin | 201 `EstProfileDto` | 400 on invalid/ImpossibleOperation |
| GET | `/api/est-profiles/{id}` | authenticated | 200 `EstProfileDto` / 404 | |
| PUT | `/api/est-profiles/{id}` | SystemAdmin | 200 `EstProfileDto` / 400 / 404 | Partial update; omitted trust lists are preserved (PR #47) |
| DELETE | `/api/est-profiles/{id}` | SystemAdmin | 204 / 404 | |

### ICD-ADM.004 Certificates and revocation (`api/certificates`) — CODE-OBSERVED

| Method | Path | Authorization | Success | Notes |
|---|---|---|---|---|
| POST | `/api/certificates/{id}/revoke` | DeviceAdmin | 200 `{"status": "revoked"}` | Body `{"reason": "<RevocationReason>"}`; invalid reason (or `RemoveFromCrl`) → 400; failure returns the connector/service status (404/409/501/503) |

Revocation reasons enum (CODE-OBSERVED, `RevocationReason`): parsed
case-insensitively, must be a defined value, `RemoveFromCrl` explicitly rejected
by the API.

### ICD-ADM.005 Users (`api/users`) — CODE-OBSERVED (controller-level SystemAdmin)

| Method | Path | Notes |
|---|---|---|
| GET | `/api/users` | 200 `UserDto[]` |
| GET | `/api/users/{id}` | 200 / 404 |
| POST | `/api/users` | 201 `UserDto`; 400 invalid; 409 conflict (duplicate/last-admin rules) |
| PUT | `/api/users/{id}` | 200; role/active/password change; blank password preserves current (DOCUMENTED) |
| DELETE | `/api/users/{id}` | 204 / 400 (self-delete or last active SystemAdmin blocked) / 404 |

### ICD-ADM.006 API keys (`api/users/{userId}/api-keys`) — CODE-OBSERVED

| Method | Path | Authorization | Notes |
|---|---|---|---|
| GET | `/api/users/{userId}/api-keys` | authenticated; self or SystemAdmin (else 403) | 200 `ApiKeyDto[]` |
| POST | `/api/users/{userId}/api-keys` | DeviceAdmin; self or SystemAdmin | 201 `GenerateApiKeyResponseDto` (raw key shown once); 400/403 |
| DELETE | `/api/users/{userId}/api-keys/{keyId}` | self or SystemAdmin | 204 revoked / 403 / 404 |

### ICD-ADM.007 Auth (`api/auth`) — CODE-OBSERVED

| Method | Path | Authorization | Notes |
|---|---|---|---|
| GET | `/api/auth/status` | anonymous | 200 `AuthStatusDto{mode, username?, role?, userId?}` |
| POST | `/api/auth/setup` | anonymous | 200 `AuthResponseDto{token, expiresAt, role, username, userId}`; 409 when setup already completed; 400 invalid |
| POST | `/api/auth/login` | anonymous | 200 `AuthResponseDto`; 401 generic "Invalid username or password." |
| POST | `/api/auth/logout` | authenticated | 204 (JWT is stateless; client discards token) |
| GET | `/api/auth/me` | authenticated | 200 `{id, username, role}` |

### ICD-ADM.008 Gateway settings (`api/settings/gateway`) — CODE-OBSERVED

| Method | Path | Authorization | Notes |
|---|---|---|---|
| GET | `/api/settings/gateway` | authenticated | 200 `GatewaySettingsDto` (`defaultCertificateLifetimeHours`, `minCertificateLifetimeHours`, `maxCertificateLifetimeHours`, timestamps) |
| PUT | `/api/settings/gateway` | SystemAdmin | 200 `GatewaySettingsDto`; 400 invalid |

### ICD-ADM.009 Notification settings (`api/settings/notifications`) — CODE-OBSERVED

| Method | Path | Authorization | Notes |
|---|---|---|---|
| GET | `/api/settings/notifications` | authenticated | 200 `NotificationSettingsDto` (SMTP host/port, `tlsMode`, `authMode`, `hasPassword` — plaintext never returned) |
| PUT | `/api/settings/notifications` | SystemAdmin | 200; `password` null=preserve, empty=clear, non-empty=store encrypted |
| GET | `/api/settings/notifications/recipients` | authenticated | 200 `NotificationRecipientDto[]` |
| POST | `/api/settings/notifications/recipients` | SystemAdmin | 200 upsert `NotificationRecipientDto`; 400 |
| DELETE | `/api/settings/notifications/recipients/{id}` | SystemAdmin | 204 / 404 |
| POST | `/api/settings/notifications/test` | SystemAdmin | 204 on success; 400 on failure (draft settings, stored password used when `Password` null) |

### ICD-ADM.010 Status and health (`api/status`) — CODE-OBSERVED

| Method | Path | Authorization | Notes |
|---|---|---|---|
| GET | `/api/status/enrollments?profileId=&limit=` | authenticated | 200 `EnrollmentEventDto[]`; invalid profileId → 400; limit clamped 1–1000, default 50 |
| GET | `/api/status/health` | anonymous (health-check middleware) | 200 healthy |
| GET | `/api/status/logs/recent?sinceSeq=&limit=` | authenticated | 200 `LiveLogPageDto` (in-memory live-log ring buffer; pollable cursor protocol; limit clamped 1–1000, default 200) |

### ICD-ADM.011 CRL distribution (`api/crl`) — CODE-OBSERVED

| Method | Path | Authorization | Notes |
|---|---|---|---|
| GET | `/api/crl/{issuerFingerprint}.crl` | **anonymous** (`AllowAnonymous`) | 200 DER CRL, Content-Type `application/pkix-crl`, `Cache-Control: public, max-age=<seconds until NextUpdate>`; 404 when no CRL exists for the issuer fingerprint |

### ICD-ADM.012 ACME challenge (`/.well-known/acme-challenge`) — CODE-OBSERVED

| Method | Path | Authorization | Notes |
|---|---|---|---|
| GET | `/.well-known/acme-challenge/{token}` | anonymous | 200 `text/plain` key authorization; 404 unknown token |

---

## 11. OpenAPI file status and TO-BE contract fragment — AS-IS contract-first designs

**Provenance:** CODE-OBSERVED (file inspection). The repository-root OpenAPI files
are partial, contract-first designs without an `openapi:` root document; they
describe intended Admin resources and are **not** a verified description of the
implemented behavior:

| File | Status | Content |
|---|---|---|
| `OpenAPI.Admin.yaml` | AS-IS contract-first design; paths match `CaBackendsController` (list/create/get/put/delete, `generate-selfsigned` documented in detail) | No security scheme declaration; no `/active`, `/test`, `/activate` paths |
| `OpenAPI.ESTProfiles.yaml` | AS-IS contract-first design; matches `EstProfilesController` CRUD | No security scheme declaration |
| `OpenAPI.StatusObserve.yaml` | AS-IS contract-first design; `/api/status/health`, `/api/status/enrollments` | Matches `StatusController` |
| `OpenAPI.Schema.yaml` | AS-IS shared schemas (`CaBackend`, `EstProfile`, `EnrollmentEvent`); **stale** relative to DTOs (missing `hostnameMatchType`, `validityDays`, `requireClientCertificate`, trust-policy fields, `isEnabled/isActive`) | Drift finding |
| `OpenAPI.Client.yaml` | **Empty file (0 bytes)** | No content; interface UNKNOWN; drift finding |

The `EstProfile` DTO (CODE-OBSERVED, `EstProfileDto.cs`) is the authoritative
AS-IS resource shape. A fully-specified admin resource fragment — **TO-BE
contract (PROPOSED, OpenAPI 3.1)** for review as the approved EST-profile
management contract:

```yaml
openapi: 3.1.0
info: { title: Kryptonian Admin API — EST Profiles, version: 0.1.0-draft }
components:
  securitySchemes:
    ApiKey: { type: apiKey, in: header, name: X-API-Key }
    JwtBearer: { type: http, scheme: bearer }
  schemas:
    EstProfile:
      type: object
      properties:
        id: { type: string }
        name: { type: string }
        hostnames: { type: array, items: { type: string } }
        hostnameMatchType: { type: string, enum: [exact, suffix, wildcard] }
        allowedWildcardSuffix: { type: string }
        pathPrefix: { type: string, default: /.well-known/est }
        caBackendId: { type: string }
        certificateTemplate: { type: string }
        allowedKeyUsages: { type: array, items: { type: string } }
        validityDays: { type: integer, default: 365 }
        requireClientCertificate: { type: boolean, default: true }
        validateClientCertificateChain: { type: boolean, default: false }
        trustedClientCaThumbprints: { type: array, items: { type: string } }
        isEnabled: { type: boolean }
        createdAt: { type: string, format: date-time }
        updatedAt: { type: string, format: date-time }
paths:
  /api/est-profiles:
    get:
      security: [{ ApiKey: [] }, { JwtBearer: [] }]
      responses:
        '200':
          description: Profiles
          content:
            application/json:
              schema: { type: array, items: { $ref: '#/components/schemas/EstProfile' } }
    post:
      security: [{ ApiKey: [] }, { JwtBearer: [] }]
      x-required-role: SystemAdmin
      responses:
        '201': { description: Created }
        '400': { description: Invalid profile }
        '401': { description: Authentication required }
        '403': { description: SystemAdmin role required }
```

---

## 12. Error model and error codes (ICD-ERR.001)

**Provenance:** CODE-OBSERVED (controllers); DOCUMENTED (`docs/API-GUIDE.md`).

AS-IS error body shapes (all `application/json`):

- EST endpoints: `{"error": "<message>"}` and, for pending,
  `{"error": "<message>", "retryAfter": <seconds>}`.
- Admin endpoints: `{"error": "<message>"}` from controller catch blocks.
- API guide documents an extended shape `{"error", "details", "correlationId"}`;
  correlation/request IDs are injected by `RequestIdMiddleware` into logging
  context. Whether the extended shape is emitted by any endpoint is
  **UNKNOWN** (the guide is DOCUMENTED-only for that shape; controllers observed
  emit `{error}` only).

AS-IS status-code semantics across both APIs:

| Code | Meaning | Typical cause |
|---|---|---|
| 200 | Success | |
| 201 | Created | POST resource creation |
| 202 | Accepted (pending) | Async issuance; check `Retry-After` |
| 204 | No Content | Delete / logout / test notification |
| 400 | Bad Request | Invalid CSR, invalid body, invalid arguments |
| 401 | Unauthorized | Missing/invalid API key, JWT, client certificate, activation credential |
| 403 | Forbidden | Role policy failure, cert validation failure, Basic-over-HTTP, untrusted cert-forwarding proxy |
| 404 | Not Found | Unknown profile/backend/device/certificate/id; unknown label |
| 409 | Conflict | Setup already done; already revoked; issuer fingerprint mismatch; CA rotation |
| 413 | Payload Too Large | CSR > 64 KiB |
| 426 | Upgrade Required | EST over plain HTTP |
| 429 | Too Many Requests | Rate limit exceeded |
| 500 | Server Error | Unexpected failures; crypto generation failure |
| 501 | Not Implemented | Remote-CA revocation unsupported (fail-closed signal, §14) |
| 503 | Service Unavailable | CA backend unavailable/expired |

TO-BE/PROPOSED: a single canonical error schema with stable machine-readable
codes and correlationId for both APIs (SR-ADM, SR-F-INT); not implemented at
baseline.

---

## 13. Certificate and CSR formats (ICD-PCR.001–ICD-PCR.003)

**Provenance:** CODE-OBSERVED (`PkcsService.cs`, `CertificateDto.cs`, `CrlController.cs`).

ICD-PCR.001 — PKCS#10 CSR ingestion (AS-IS):

- Accepted encodings: raw DER; base64; PEM with headers
  `-----BEGIN CERTIFICATE REQUEST-----` or legacy
  `-----BEGIN NEW CERTIFICATE REQUEST-----`.
- The CSR **signature is always verified** (`Pkcs10CertificationRequest.Verify`);
  verification failure throws `CryptographicException` and the enrollment is
  rejected (CODE-OBSERVED).
- Parsed CSR exposes (`ParsedCsr`): `RawData` (DER), `SubjectDn`, `PublicKey`,
  `PublicKeyAlgorithm` (RSA / ECDSA), `KeySize`, `SignatureAlgorithm`,
  `SubjectAlternativeNames` (DNS/IP/email/URI extracted from the extension-request
  attribute).
- Enrollment policy additionally enforces CSR and returned-certificate policy
  (subject-DN rules per profile, key-usage enforcement) — PR #43
  (DOCUMENTED, `docs/IMPLEMENTATION-TRACKER.md`).

ICD-PCR.002 — Returned certificates and chains (AS-IS):

- EST responses: PKCS#7 degenerate SignedData, certs-only, base64-encoded
  (`application/pkcs7-mime`), containing the leaf certificate and the issuer
  chain returned by the connector.
- Admin API representation (`CertificateDto`): PEM string
  (`certificatePem`), optional DER base64 (`certificateDerBase64`),
  `serialNumber`, `subjectDn`, `issuerDn`, `thumbprint`, validity window,
  `caBackendId`, `caBackendType`, `gatewayOid` (private OID arc
  `1.3.6.1.4.1.99999.*` used for identity binding, CODE-OBSERVED).

ICD-PCR.003 — CRL format (AS-IS):

- Issuer-scoped, signed DER CRLs (RFC 5280), Content-Type
  `application/pkix-crl`, keyed by SHA-256 issuer fingerprint
  (`/api/crl/{issuerFingerprint}.crl`).
- CRL state persisted per issuer (`issuer_crl_states`: fingerprint unique,
  monotonically increasing `crl_number`, thisUpdate/nextUpdate, base64 DER).
- AS-IS prototype CRL lifetime is **60 seconds** (`PrototypeCrlLifetime`) or the
  issuer's NotAfter, whichever is earlier — a deliberate prototype cadence
  (CODE-OBSERVED comment); production cadence is a TO-BE decision.
- Revocation persistence is transactional: state update and revocation commit
  are wrapped in a unit-of-work transaction with rollback on failure
  (CODE-OBSERVED; PR #46).

---

## 14. Internal CA connector contract (ICD-CAI.001) — language-neutral contract

**Provenance:** CODE-OBSERVED (`ICaConnector.cs`, `ICaConnectorFactory.cs`,
value objects, connector implementations). Signatures are recorded as a
language-neutral contract; the C# interface is the reference implementation.

Contract — CA connector (per CA backend):

```
operation Type() -> CaBackendType          {SelfSigned | Acme | Adcs | Ejbca | ...}
operation GetCaCertificatesAsync(ct) -> X509Certificate2[]
    outputs: ordered CA chain for the backend's issuer
    failure: empty array (ACME/EJBCA pre-issuance) or exception (caller maps
             to 404/403/503)
operation IssueCertificateAsync(csr: ParsedCsr, profile: EstProfile, ct)
    -> CertificateIssuanceResult
    outputs: Successful(leafCert, chain) | Failed(message)
    failure semantics: never throws for backend rejection; returns Failed
                       with a message; gateway logs and maps to HTTP status
operation RevokeCertificateAsync(serial, reason, ct) -> bool
    semantics: native remote-CA revocation mutation only; unsupported
               backends return false and log a warning (never silent success)
operation GenerateCrlAsync(request: CrlGenerationRequest, ct)
    -> CrlGenerationResult
    default: Unsupported() (Supported=false, no DER)
    semantics: builds a complete signed CRL for the connector's current
               issuer; must echo ExpectedIssuerFingerprint on success
operation TestConnectionAsync(ct) -> bool
    semantics: best-effort reachability probe (GET /health)
```

Contract — connector factory:

```
operation CreateConnector(backend: CaBackend) -> ICaConnector
    failure: NotSupportedException for unimplemented backend types
             (Cfssl, HashiCorpVault, Smallstep, OpenXpki) — fail-closed;
             ArgumentException for unknown type
    caching: one connector instance per backendId (memoized, thread-safe)
operation GetCachedConnector(backendId) -> ICaConnector?
```

### ICD-CAI.002 Per-connector capabilities table — AS-IS (CODE-OBSERVED)

| Capability | Self-Signed | ACME | ADCS | EJBCA |
|---|---|---|---|---|
| Implementation | `Crypto/SelfSignedCaConnector.cs` (local file-based CA) or `Harness/SelfSignedEstCaConnector.cs` when a harness base URL is configured | `Acme/AcmeCaConnector.cs` (Certes library) | `Harness/AdcsCaConnector.cs` (SCEP client) | `Harness/EjbcaCaConnector.cs` (REST client) |
| `GetCaCertificatesAsync` | Local CA cert(s) from PFX/PEM; harness PEM list | Cached chain from last issuance; well-known ISRG/ZeroSSL/BuyPass chain fallback | SCEP `GetCACert` (first cert of signed-data response) | Per-instance cached chain only; fresh scoped connector starts empty, even after issuance in another request |
| `IssueCertificateAsync` | Local signing (BouncyCastle); harness EST relay when harness configured | Full ACME order flow (http-01/dns-01, optional EAB) | SCEP `PKIOperation` with PKCSReq | REST `POST /ejbca/ejbca-rest-api/v1/certificate/pkcs10enroll` |
| Native remote revocation | **No** (returns false) | No revocation path exercised in inspected code | **No** (returns false; revoke in ADCS or add protocol integration) | **Yes** (`PUT /certificate/{issuerDn}/{serial}/revoke`, requires `IssuerDn` config; false otherwise) |
| `GenerateCrlAsync` | Supported (gateway-side signed CRL) | Default → **Unsupported** | Default → **Unsupported** | Default → **Unsupported** |
| Revocation fail-closed behavior | Revocation proceeds via gateway-signed CRL | Revocation requests fail closed (CRL generation unsupported → error, no silent success) | **Fail closed**: remote revocation unsupported and CRL generation unsupported → revocation fails | **Fail closed** unless native revoke succeeds; CRL generation still unsupported → 409/501/503-class failure |
| Key/credential configuration | PfxPath+PfxPassword or CertPath+KeyPath; harness: HarnessBaseUrl only | DirectoryUrl, Email, PreferredChallengeType, EabKeyId+EabHmacKey | BaseUrl (SCEP), TemplateName, ValidityDays, EnrollmentAgentPfx/PfxPassword/Thumbprint (mTLS to NDES) | BaseUrl (REST), CertificateProfile, EndEntityProfile, IssuerDn, ValidityDays |
| Connector-specific limitations | Private CA key must be exportable for BouncyCastle signing (hardening note: HSM-backed keys for production, CODE-OBSERVED comment) | HTTP-01 needs publicly reachable `/.well-known/acme-challenge/`; dns-01 stub (`Dns01ChallengeStub`) not a production DNS integration; `/cacerts` empty on a fresh connector instance | ADCS hospital interoperability **unverified** (DOCUMENTED, tracker); SCEP chain limited to the returned certificate | EJBCA hospital interoperability **unverified** (DOCUMENTED, tracker); chain empty pre-issuance; issuer rotation conflicts rejected (409) |

ICD-CAI.003 — Revocation orchestration failure semantics — AS-IS
(CODE-OBSERVED, `CertificateRevocationService`):

- Certificate missing → 404; already revoked → 409.
- Certificate with no CA backend → 501; connector type unimplemented → 501.
- Backend disabled → 503; issuer certificate expired → 503.
- Issuer mismatch / leaf-does-not-chain failure / fingerprint-vs-backend
  mismatch / rotated CA certificate → 409 (fail closed, no partial state).
- `GenerateCrlAsync` returning `Unsupported`, a null/empty DER, or a mismatched
  issuer fingerprint causes the operation to fail (fail closed). Unsupported
  remote-CA revocation therefore **fails closed** — consistent with
  `docs/IMPLEMENTATION-TRACKER.md` (DOCUMENTED).

### Enrollment orchestrator contract (ICD-CAI.004) — AS-IS (CODE-OBSERVED)

```
operation GetCaCertsAsync(profileId, ct) -> byte[]  (PKCS#7 certs-only)
    rejects: profile missing/disabled, backend missing/disabled,
             empty chain -> InvalidOperationException (404-mapped)
operation EnrollAsync(profileId, csrBytes, deviceId?, clientIp, ct,
                      activationCode?, manufacturer?, model?, serial?)
    -> EnrollmentResult {Success|Failed(status)|Pending(retryAfter)}
    semantics: validates activation credential, CSR policy, subject-CN
               uniqueness; persists EnrollmentEvent; dispatches rejection
               notifications
operation EnrollAdminAsync(profileId, deviceRecordId, csrBytes, clientIp, ct)
    -> EnrollmentResult    (admin workflow, not public EST)
operation ReenrollAsync(profileId, csrBytes, existingCert, clientIp, ct)
    -> EnrollmentResult
    semantics: authenticates with the existing certificate, preserves the
               approved identity, issues a new key-bound certificate
```

---

## 15. Database integration interfaces (ICD-DAT.001) — AS-IS

**Provenance:** CODE-OBSERVED (`Program.cs`, `KryptonianDbContext.cs`,
`SchemaUpgrades.cs`); DOCUMENTED (`docs/IMPLEMENTATION-TRACKER.md`).

- EF Core providers: `Kryptonian:Database:Provider` = `Sqlite` | `PostgreSQL` |
  `InMemory`. When unset: PostgreSQL if a connection string is present,
  otherwise InMemory (with a startup warning). Unknown values fail startup.
- Connection string: `ConnectionStrings:DefaultConnection` or
  `Kryptonian:Database:ConnectionString`. SQLite default (no config):
  `Data Source=kryptonian.db;Cache=Shared`. PostgreSQL without a connection
  string fails startup. PostgreSQL uses `NpgsqlDataSourceBuilder` with dynamic
  JSON enabled (dictionary config columns).
- **Schema creation/upgrades:** existing installations use
  `EnsureCreated`, not EF migrations. `SchemaUpgrades.EnsureTrustPolicyAsync`
  performs **additive-only** upgrades (never recreates a populated database):
  - PostgreSQL: `ALTER TABLE ... ADD COLUMN IF NOT EXISTS` for
    `est_profiles.validate_client_certificate_chain`,
    `est_profiles.trusted_client_ca_thumbprints`,
    `certificates.revoked_at`, `certificates.revocation_reason`;
    unique index `ix_certificates_issuer_serial (ca_backend_id, serial_number)`
    replacing the serial-only index; `issuer_crl_states` table
    (`CREATE TABLE IF NOT EXISTS`) with unique issuer fingerprint and
    unique per-backend index.
  - SQLite: same columns/tables via `PRAGMA table_info` existence checks and
    `ALTER TABLE ADD COLUMN` / `CREATE TABLE IF NOT EXISTS`.
- `KryptonianDbContextFactory` provides design-time context creation.
- Repository access is through `IUnitOfWork` (aggregate of typed repositories
  plus transactional primitives `BeginTransactionAsync`/`Commit`/`Rollback`/
  `TryPersistRevocationStateAsync`).
- Known boundary (DOCUMENTED): PostgreSQL schema upgrades are implemented but
  only SQLite persistence was exercised at baseline.

---

## 16. Configuration interfaces (ICD-CFG.001–ICD-CFG.002)

**Provenance:** CODE-OBSERVED (`Program.cs`, `ApiKeyAuthenticationHandler.cs`,
`appsettings.json`, `CaConnectorFactory.cs`); DOCUMENTED (`docs/ca-backend-configuration.md`,
`docs/user-management.md`).

ICD-CFG.001 — Configuration keys (appsettings `Kryptonian:*`; environment
equivalents use the double-underscore convention):

| Key / env var | Meaning | AS-IS default / behavior |
|---|---|---|
| `Kryptonian:Database:Provider` | Persistence provider | unset → PostgreSQL if connection string present, else InMemory |
| `Kryptonian:Database:ConnectionString` | Connection string | none; `ConnectionStrings:DefaultConnection` preferred |
| `KRYPTONIAN__ADMINAPI__APIKEYS` / `Kryptonian:AdminApi:ApiKeys` | Static API keys (comma-separated) acting as SystemAdmin | none; with none set, users rely on JWT or DB keys (backward-compatible interface, DOCUMENTED) |
| `Kryptonian:AdminApi:AllowAnonymousInDevelopment` | Dev bypass when no static keys | false; only active in Development |
| `Kryptonian:Auth:JwtSecret` / `KRYPTONIAN__AUTH__JWTSECRET` | JWT signing key | **falls back to a hard-coded development secret** — finding for KSPEC-010 (security) |
| `Kryptonian:Auth:JwtLifetimeHours` | JWT lifetime | 8 hours (DOCUMENTED) |
| `Kryptonian:Auth:InitialAdminUsername/Password/Email` (env: `KRYPTONIAN__AUTH__INITIAL_ADMIN_*`) | First-admin bootstrap seeding | optional; seeds only when setup is pending |
| `Kryptonian:Tls:RequireTls12` | Minimum TLS version | true |
| `Kryptonian:Tls:EstPort` / `Tls:AdminPort` | Listener ports | 8443 / 8080 |
| `Kryptonian:Tls:ClientCaFiles` | PEM CA files for strict client-cert trust at the listener | none (validation then per-profile) |
| `Kryptonian:Tls:RedirectHttpToHttps` | HTTPS redirection | enabled outside Development |
| `Kryptonian:Est:AllowPlainHttp` | Dev escape hatch for HTTP EST | false |
| `Kryptonian:CertificateForwarding:KnownProxies` | IP allow-list for `X-Forwarded-Client-Cert` forwarding | empty → header rejected (fail closed) |
| `Kryptonian:Ui:AllowedOrigins` | CORS origins for the dashboard | localhost:5173 variants |
| `Kryptonian:Sandbox:Enabled` | Windows sandbox/installer mode | false; sandbox requires Production environment |
| `Kryptonian:CA:SelfSigned:*` (`KRYPTONIAN__CA__SELFSIGNED__PFXPATH/PFXPASSWORD/CERTPATH/KEYPATH/CRLDISTRIBUTIONPOINTURL`) | Self-signed CA fallbacks | used when backend JSON config omits the property |
| `KRYPTONIAN__CA__ADCS__SCEPBASEURL`, `__ADCS__ENROLLMENTAGENT*` | ADCS connector fallbacks | required unless backend URL/config set |
| `KRYPTONIAN__CA__EJBCA__RESTBASEURL`, `__EJBCA__ISSUERDN` | EJBCA connector fallbacks | required unless backend URL/config set |
| `KRYPTONIAN__CA__HARNESS__BASEURL` | Harness base URL for the self-signed harness relay | optional |
| `KRYPTONIAN__ACME__DIRECTORYURL/EMAIL/EABKEYID/EABHMACKEY/CHALLENGETYPE` | ACME connector fallbacks | Let's Encrypt production directory fallback |
| `IpRateLimiting:*` | AspNetCoreRateLimit rules (§8) | see §8 |

ICD-CFG.002 — Secret-handling rules (AS-IS):

- SMTP passwords: stored encrypted (ASP.NET Data Protection, application name
  `RTSec.Kryptonian`; persisted to disk in sandbox mode), never returned by the
  API (`hasPassword` boolean only), CODE-OBSERVED.
- ACME account keys: encrypted at rest via `IDataProtectionService`
  (CODE-OBSERVED).
- Activation codes: stored hashed, shown once via `DeviceActivationCodeDto`
  (DOCUMENTED).
- API keys: stored as SHA-256 hashes with a visible 8-char prefix only; raw key
  shown once at generation (CODE-OBSERVED).
- Backend config dictionaries may contain PFX passwords in plaintext in the
  database (CODE-OBSERVED DTO/`config` passthrough) — finding for KSPEC-006/010.

---

## 17. External system dependencies (ICD-EXT.001–ICD-EXT.004)

**Provenance:** CODE-OBSERVED unless noted.

ICD-EXT.001 — SMTP (notifications): MailKit-based `MailKitSmtpSender`;
settings via the notification-settings API (`starttls`/TLS modes, auth modes,
`TrustServerCertificate`); used for enrollment-rejection and near-expiry
notifications (`CertificateExpiryWatcher` hosted service). Dependency surface
only; gateway function does not depend on SMTP availability (best-effort
notification dispatch, CODE-OBSERVED).

ICD-EXT.002 — ACME directories: any RFC 8555 directory; well-known constants
for Let's Encrypt production/staging, ZeroSSL, BuyPass (test/production).
Requires reachable `/.well-known/acme-challenge/{token}` on the gateway for
http-01, or a DNS-01 provider (stub only). External Account Binding (EAB)
supported.

ICD-EXT.003 — ADCS/NDES (SCEP): endpoints
`GET {base}/scep/adcs?operation=GetCACert` and
`POST {base}/scep/adcs?operation=PKIOperation`
(`application/x-pki-message`); optional enrollment-agent mTLS credential
(PFX path/password or machine-store thumbprint) required when NDES enforces
challenge mode; health probe `GET {base}/health`. Interoperability with real
ADCS is **unverified** (DOCUMENTED).

ICD-EXT.004 — EJBCA (REST): `POST {base}/ejbca/ejbca-rest-api/v1/certificate/pkcs10enroll`,
`PUT {base}/ejbca/ejbca-rest-api/v1/certificate/{issuerDn}/{serial}/revoke`;
health probe `GET {base}/health`. Interoperability with real EJBCA is
**unverified** (DOCUMENTED).

Additional: the Self-Signed *harness* relay targets a local harness service
(`GET api/backends/selfsigned/cacerts`, `POST est/selfsigned/simpleenroll`) —
a test-harness dependency, not a production backend.

---

## 18. Versioning and interoperability requirements (ICD-VER.001–ICD-VER.003)

ICD-VER.001 — API versioning policy — AS-IS observation + TO-BE/PROPOSED
(CODE-OBSERVED; policy text PROPOSED):

- AS-IS: no URL or header versioning; Swagger declares a single `v1` document.
  Admin API changes are currently implicitly additive (new optional DTO fields).
- TO-BE/PROPOSED (SR-ADM, SR-F-INT): adopt a versioning policy (e.g., URL-prefix
  `/api/v{n}` or a documented additive-only contract with deprecation windows)
  before any breaking Admin API change; publish OpenAPI as the single source of
  truth. Not approved at baseline.

ICD-VER.002 — Backwards-compatibility rules for EST clients — PROPOSED
(SR-F-INT, SR-EST):

- Approved-direction rules (subject to review): never remove or repurpose the
  implemented EST paths, content types (`application/pkcs10` requests,
  `application/pkcs7-mime` certs-only responses), or the `Retry-After` pending
  contract; unknown `{label}` handling (404, no fallback) is a deliberate
  contract, not a bug; `text/plain` CSR acceptance is a leniency that may be
  narrowed but not made mandatory for clients.
- The bundled sample device client (`src/Kryptonian.MedicalDevice/Enrollment/*`)
  is a reference client, not a product deliverable (KSPEC-000 §3); its wire
  behavior does not define the platform contract.

ICD-VER.003 — Deprecated legacy activation headers — **AS-IS compat only,
NOT a mandatory interoperability requirement**:

- `X-Activation-Code`, `X-Device-Manufacturer`, `X-Device-Model`,
  `X-Device-Serial-Number` are retained in the baseline for compatibility with
  the existing sample device client (`docs/IMPLEMENTATION-TRACKER.md`,
  Deliberate boundaries: "The independent EST path uses standard Basic
  authentication; the existing sample device client retains legacy activation
  headers for compatibility. They are not mandatory for other clients and must
  not become a new interoperability requirement." — DOCUMENTED).
- Accordingly, this ICD records them as AS-IS compatibility behavior only;
  KSPEC-003 shall not derive an SR-F-INT requirement mandating them, and any
  partner-facing EST client profile shall be built on HTTP Basic bootstrap +
  native TLS client certificate re-enrollment.

---

## 19. Traceability summary (area-level; concrete bindings in KSPEC-009)

| This document | Requirement prefix area | Owner |
|---|---|---|
| §3–§9 EST interfaces | SR-EST, SR-A, SR-S, SR-F-INT | KSPEC-003 |
| §10–§11 Admin REST and OpenAPI contracts | SR-ADM, SR-Z | KSPEC-003 |
| §12 Error model | SR-ADM, SR-F-INT | KSPEC-003 |
| §13 Certificate/CSR/CRL formats | SR-C, SR-D | KSPEC-003 |
| §14 CA connector contract | SR-CAI, SR-C | KSPEC-003 |
| §15 Database interfaces | SR-D, SR-B | KSPEC-003 |
| §16 Configuration interfaces | SR-B, SR-S | KSPEC-003 |
| §17 External dependencies | SR-F-INT, SR-X | KSPEC-003 |
| §18 Versioning/interoperability | SR-F-INT, SR-EST | KSPEC-003 |

## 20. Open items for review

1. OpenAPI.Schema.yaml drift vs DTOs; OpenAPI.Client.yaml is empty (0 bytes).
2. EST paths absent from all repository OpenAPI files (fragment provided in §5).
3. Extended error shape (`details`, `correlationId`) documented but not observed
   emitted by controllers.
4. `POST /api/cas/{id}/test` lacks a DeviceAdmin/SystemAdmin role policy
   (authentication only).
5. JWT secret falls back to a hard-coded development value.
6. `POST /api/device-requests` is anonymous by design (self-service approval
   request) — confirm intended authorization model.
7. Pending-response trigger conditions per CA backend not fully characterized.
8. 60-second prototype CRL lifetime requires production policy decision.
9. Whether a no-credential simpleenroll can succeed on an open profile
   (UNKNOWN, §4.4).
