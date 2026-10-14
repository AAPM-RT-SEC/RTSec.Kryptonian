# Kryptonian Software Requirements Specification

| Field | Value |
|---|---|
| Document title | Software Requirements Specification (SR-*) |
| Document ID | KSPEC-003 |
| Version | 0.2 (Draft) |
| Review status | Draft — pending review |
| Created | 2026-10-13 |
| Baseline repository commit | `682f996c8447afec13bc1fce5b7f59e6a06efa25` (branch `feat/windows-sandbox-installer`, "Release Windows gateway and portable enrollment demo 0.1.4") |
| Repository | https://github.com/AAPM-RT-SEC/RTSec.Kryptonian |
| Revision history | 0.1 — 2026-10-13 — Initial draft: 154 software requirements elaborated from KSPEC-002 UR-001 – UR-038 and source inspection of the baseline commit (Author: requirements engineering team). |
| Source references | KSPEC-000 (conventions); KSPEC-001 (product definition); KSPEC-002 (user requirements UR-001 – UR-038); docs/ARCHITECTURE.md; docs/API-GUIDE.md; docs/device-enrollment.md; docs/ca-backend-configuration.md; docs/user-management.md; docs/email-notifications.md; docs/admin-setup.md; docs/DEPLOYMENT.md; docs/IMPLEMENTATION-TRACKER.md; docs/formal-specification/04-SOFTWARE-DESIGN.md; docs/formal-specification/05-INTERFACE-CONTROL.md; docs/formal-specification/07-OPERATIONAL-RISK.md; src/RTSec.Kryptonian.{Api,Application,Domain,Infrastructure}; OpenAPI.*.yaml; artifacts/developer/verify-*/evidence.json |
| Author | Name: TBD — Date: TBD — Signature: TBD |
| Reviewer | Name: TBD — Date: TBD — Signature: TBD |
| Approver | Name: TBD — Date: TBD — Signature: TBD |
| Reconciliation revision | 0.2 - 2026-10-13 - source/document audit and publication controls (KSPEC-016) |

This document is the **principal normative specification** of the RTSec.Kryptonian
Certificate Enrollment Gateway. It elaborates the stakeholder requirements of
KSPEC-002 into software requirements (SR-*) using the requirement record format of
KSPEC-000 §8 and the identifier scheme of KSPEC-000 §7. This document maintains the
**canonical Requirements Register** (Section 21) of all normative SR IDs; every other
package document references requirements by ID only and shall not redefine them
(KSPEC-000 §7).

**Product classification (binding, KSPEC-000 §3).** Kryptonian is enterprise
healthcare IT cybersecurity infrastructure: an EST (RFC 7030) certificate enrollment,
identity provisioning, and credential lifecycle gateway. It is **not a regulated
medical device**, performs no clinical functions, and no requirement in this document
may be read as claiming FDA classification, IEC 62304 lifecycle classification,
ISO 14971 hazard coverage, SDC conformity, DICOM TLS certification, or any other
standards-conformance status. Device identity, enrollment authorization, network
trust, and application-layer authorization are distinguished throughout; no gateway
state derives clinical capability.

---

## 1. Requirement conventions

Each requirement is presented with the full field set of KSPEC-000 §8:

| Field | Meaning |
|---|---|
| ID | Stable identifier `<PREFIX>-<AREA>.<nnn>` (never reused, KSPEC-000 §7). |
| Requirement | A single verifiable obligation using "shall". |
| Rationale | Why the obligation exists. |
| Source | Upstream UR (KSPEC-002) and/or KSPEC-001 element and/or finding driving the requirement. |
| Classification | AS-IS, TO-BE, or PROPOSED behavior classification (KSPEC-000 §4). |
| Priority | Critical / High / Medium / Low. |
| Status | Existing / Partial / Missing / Proposed (KSPEC-000 §6). |
| Verification | T = Test, I = Inspection, A = Analysis, D = Demonstration (KSPEC-000 §11). |
| Evidence | Concrete repository paths, documents, or executed-evidence locations, each annotated with its provenance class (KSPEC-000 §5). |
| Approval | Draft or Review Required. Approval is never inferred from implementation (KSPEC-000 §8). |

Rules applied throughout:

1. **Single obligation.** Each requirement states exactly one verifiable obligation.
   Compound behaviors are split.
2. **Implementation independence.** Requirements state observable system behavior,
   not algorithms or class names, except where a configuration key or protocol
   literal is itself the contract.
3. **Weak controls are not reproduced.** Prototype defects and weak controls observed
   at baseline (hard-coded JWT secret fallback, endpoints with authentication but no
   role policy, empty/stale OpenAPI contract files, hard-coded demo CRL lifetime,
   DNS-01 challenge stub, forwarder trust by source IP only) are specified as correct
   TO-BE requirements with Status **Missing** or **Proposed**, never as intended
   AS-IS behavior (KSPEC-000 §4).
4. **Unconfirmed thresholds use `TBD`** with a separate recommendation and rationale
   (KSPEC-002 §16.5). No threshold value is presented as agreed without evidence.
5. **Legacy activation headers are non-normative** (KSPEC-002 §16.3): the
   `X-Activation-Code`/`X-Device-*` compatibility headers are specified as
   compatibility behavior only and never as an interoperability requirement.
6. **Revocation realism** (KSPEC-002 §16.4): fail-closed behavior for unsupported
   remote revocation is preserved; no promise of instant mid-association termination.
7. **Honest status.** Status reflects the baseline commit only, with evidence classes;
   documented-only claims are never upgraded to VERIFIED.

---

## 2. Identifier areas used in this document

| Prefix | Area | Section |
|---|---|---|
| SR-F-REG | Functional — device registration and identity management | §3 |
| SR-F-EST | Functional — EST protocol behavior | §4 |
| SR-F-CLM | Functional — certificate lifecycle management flows | §5 |
| SR-F-CAI | Functional — CA backend integration | §6 |
| SR-F-ADM | Functional — administration and portal | §7 |
| SR-F-INT | Functional — interoperability and contract artifacts | §8 |
| SR-S | Security | §9 |
| SR-A | Authentication | §10 |
| SR-Z | Authorization | §11 |
| SR-C | Certificate lifecycle policy | §12 |
| SR-D | Data management | §13 |
| SR-P | Performance | §14 |
| SR-R | Reliability | §15 |
| SR-V | Availability | §16 |
| SR-U | Audit | §17 |
| SR-B | Deployment | §18 |
| SR-O | Operational maintenance | §19 |
| SR-X | Failure and recovery | §20 |
| — | Requirements Register (canonical ID list) | §21 |

---

## 3. Functional requirements — device registration and identity management (SR-F-REG)

### SR-F-REG.001 — Administrator device registration with one-time activation code

- **ID:** SR-F-REG.001
- **Requirement:** The gateway shall allow an administrator to register a device by entering only an administrative alias and shall issue a one-time activation code for that device without requiring manual entry of the device certificate identity.
- **Rationale:** Minimizes onboarding effort and transcription errors; device identity is supplied authoritatively by the device itself during activation.
- **Source:** KSPEC-002 UR-001 (UC-01, SCN-01).
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** D
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/DevicesController.cs` (Create, activation-code issuance) CODE-OBSERVED; `src/RTSec.Kryptonian.Application/Services/DeviceService.cs` CODE-OBSERVED; docs/device-enrollment.md DOCUMENTED.
- **Approval:** Review Required

### SR-F-REG.002 — Activation code secrecy, one-time use, and replay rejection

- **ID:** SR-F-REG.002
- **Requirement:** The gateway shall generate activation codes that are one-time and time-limited, shall store them only as one-way hashes, shall consume the code atomically when accepting validated bootstrap credentials, before CA issuance, and shall reject any replay of a consumed, expired, or invalid activation code.
- **Rationale:** The activation code is a bootstrap secret; replay would permit device-identity impersonation.
- **Source:** KSPEC-002 UR-002 (UC-01/UC-02, SCN-02).
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** Critical
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/EstController.cs` (bootstrap credential validation) CODE-OBSERVED; `src/RTSec.Kryptonian.Application/Services/DeviceService.cs` CODE-OBSERVED; replay rejection VERIFIED per docs/IMPLEMENTATION-TRACKER.md and `artifacts/developer/verify-*/evidence.json`.
- **AS-IS limitation:** Consumption is persisted before backend resolution, issuance-policy validation and CA issuance; a later failure, pending response or process crash can leave the device Pending with a consumed code. Administrator code regeneration is the recovery path; consumption and issuance are not a single transaction (CODE-OBSERVED: `EnrollmentOrchestrator.EnrollAsync`, `TryConsumeActivationCodeAsync`). VT-060 specifies failure/restart verification.
- **Approval:** Review Required

### SR-F-REG.003 — Centralized device inventory

- **ID:** SR-F-REG.003
- **Requirement:** The gateway shall provide an administrator-accessible device inventory that reports, for every registered device, its registration status, assigned EST profile, identity metadata, and certificate state.
- **Rationale:** Administrators need one operating surface for the device estate and its trust state.
- **Source:** KSPEC-002 UR-003 (UC-05).
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** D
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/DevicesController.cs` (List, Get, Events, AllCertificates) CODE-OBSERVED; `src/RTSec.Kryptonian.Application/DTOs/DeviceDto.cs` CODE-OBSERVED; README.md (Device Registration Center) DOCUMENTED.
- **Approval:** Review Required

### SR-F-REG.004 — Device record status lifecycle

- **ID:** SR-F-REG.004
- **Requirement:** The gateway shall record device registration status transitions (including pending, active, and removed states) together with their timestamps, and shall expose those transitions in the device event history.
- **Rationale:** Lifecycle transitions are the basis of trust decisions and audit reconstruction.
- **Source:** KSPEC-002 UR-003; docs/flow.md (device database model).
- **Classification:** TO-BE (elaborates AS-IS behavior; the AS-IS status enum covers pending/active/removed while trust state is carried as separate device trust data — see SR-Z.003).
- **Priority:** High
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Domain/Enums/DeviceStatus.cs` CODE-OBSERVED; `src/RTSec.Kryptonian.Api/Controllers/DevicesController.cs` (Events) CODE-OBSERVED.
- **Approval:** Review Required

### SR-F-REG.005 — Enrollment identity binding to the pending registration

- **ID:** SR-F-REG.005
- **Requirement:** The gateway shall bind the enrolled device identity to its pending registration by requiring that the CSR subject common name is present, unique among registered devices, and accompanied by device metadata including a serial number, and shall reject requests that fail this binding.
- **Rationale:** Prevents duplicate or ambiguous device identities and binds issued certificates to the intended registry entry.
- **Source:** KSPEC-002 UR-020 (UC-02).
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Application/Services/EnrollmentOrchestrator.cs` and `src/RTSec.Kryptonian.Application/Services/EnrollmentCertificatePolicy.cs` CODE-OBSERVED; PR 38 identity binding VERIFIED per docs/IMPLEMENTATION-TRACKER.md.
- **Approval:** Review Required

### SR-F-REG.006 — Self-service approval requests create only pending records

- **ID:** SR-F-REG.006
- **Requirement:** The gateway shall accept self-service device approval requests through a dedicated endpoint that, without an authenticated caller, creates only a pending device record requiring administrator approval, and shall never grant enrollment capability on the basis of such a request alone.
- **Rationale:** Self-service request intake is useful for estate discovery, but trust must remain an administrator decision.
- **Source:** KSPEC-002 UR-001; KSPEC-005 open item (authorization model of `POST /api/device-requests`).
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/DevicesController.cs` (RequestApproval, `[AllowAnonymous]`, 202 response) CODE-OBSERVED.
- **Approval:** Review Required

### SR-F-REG.007 — Abuse controls on the anonymous approval-request endpoint

- **ID:** SR-F-REG.007
- **Requirement:** The gateway shall apply abuse controls (rate limiting and/or submission throttling) to the anonymous self-service approval-request endpoint so that unauthenticated callers cannot flood the device registry with pending records; the production threshold is `TBD` (recommendation: extend the existing EST rate-limit configuration to this endpoint).
- **Rationale:** An unauthenticated write path without abuse controls permits registry flooding and operator alert fatigue.
- **Source:** KSPEC-005 open item 6 (anonymous endpoint by design — confirm authorization model); KSPEC-007 ORK finding (flooding exposure).
- **Classification:** TO-BE (hardening of an AS-IS weak control).
- **Priority:** Medium
- **Status:** Missing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/DevicesController.cs` (RequestApproval with no rate-limit attribute) CODE-OBSERVED; `src/RTSec.Kryptonian.Api/appsettings.json` (IpRateLimiting scoped to EST paths) CODE-OBSERVED.
- **Approval:** Review Required

### SR-F-REG.008 — Trust removal blocks further enrollment

- **ID:** SR-F-REG.008
- **Requirement:** The gateway shall, upon administrative trust removal, deny subsequent enrollment and renewal attempts for that device and shall record a blocked enrollment event including a reason.
- **Rationale:** Decommissioned or compromised devices must not acquire fresh credentials.
- **Source:** KSPEC-002 UR-009 (UC-04, SCN-05).
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** Critical
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Application/Services/EnrollmentOrchestrator.cs` (trust-policy gate) CODE-OBSERVED; docs/flow.md (Trust Removal Flow) DOCUMENTED.
- **Approval:** Review Required

### SR-F-REG.009 — Device identity metadata preservation

- **ID:** SR-F-REG.009
- **Requirement:** The gateway shall persist the device identity metadata presented during activation (including common name, manufacturer, model, and serial number) and shall display it in the device inventory.
- **Rationale:** Identity metadata is what ties a certificate back to a physical device for inventory and incident response.
- **Source:** KSPEC-002 UR-003, UR-020; README.md (activation workflow).
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** D
- **Evidence:** `src/RTSec.Kryptonian.Application/DTOs/DeviceDto.cs` CODE-OBSERVED; README.md (activation code workflow) DOCUMENTED.
- **Approval:** Review Required

### SR-F-REG.010 — History preservation on device removal

- **ID:** SR-F-REG.010
- **Requirement:** The gateway shall preserve enrollment event history for removed devices so that audit reconstruction remains possible after device deletion.
- **Rationale:** Deleting the device record must not erase the evidence of credentials it received.
- **Source:** KSPEC-002 UR-011 (audit reconstruction); KSPEC-007 (revocation/removal scenarios).
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** Medium
- **Status:** Existing
- **Verification:** I
- **Evidence:** `src/RTSec.Kryptonian.Infrastructure/Repositories/EnrollmentEventRepository.cs` (events keyed by device record) CODE-OBSERVED; `src/RTSec.Kryptonian.Api/Controllers/DevicesController.cs` (Events after removal) CODE-OBSERVED.
- **Approval:** Review Required

---

## 4. Functional requirements — EST protocol behavior (SR-F-EST)

### SR-F-EST.001 — EST endpoint surface

- **ID:** SR-F-EST.001
- **Requirement:** The gateway shall expose the EST operations `cacerts`, `simpleenroll`, and `simplereenroll` under `/.well-known/est/` and under profile-scoped paths `/.well-known/est/{label}/`, and shall route requests case-insensitively to these operations.
- **Rationale:** A stable, standards-shaped endpoint surface is the device-facing contract of the gateway.
- **Source:** KSPEC-002 UR-016, UR-022 (UC-02/UC-03); RFC 7030 (referenced as design input, not a conformance claim).
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** Critical
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/EstController.cs` (route templates) CODE-OBSERVED; docs/ARCHITECTURE.md DOCUMENTED; independent-client EST executed per docs/IMPLEMENTATION-TRACKER.md VERIFIED.
- **Approval:** Review Required

### SR-F-EST.002 — cacerts response format

- **ID:** SR-F-EST.002
- **Requirement:** The gateway shall respond to a `cacerts` request for a valid profile with the profile's CA certificate chain encoded as base64 PKCS#7 with content type `application/pkcs7-mime; smime-type=certs-only` and the header `Content-Transfer-Encoding: base64`.
- **Rationale:** Devices must obtain the trust anchors needed to validate issued chains without out-of-band CA distribution.
- **Source:** KSPEC-002 UR-017 (UC-02).
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/EstController.cs` (GetCaCerts, Pkcs7MimeType) CODE-OBSERVED; `src/RTSec.Kryptonian.Infrastructure/Crypto/PkcsService.cs` CODE-OBSERVED.
- **Approval:** Review Required

### SR-F-EST.003 — simpleenroll CSR acceptance and size limit

- **ID:** SR-F-EST.003
- **Requirement:** The gateway shall accept a PKCS#10 CSR submitted to `simpleenroll` with content type `application/pkcs10` (base64 transfer encoding), shall reject request bodies larger than 64 KiB with HTTP 413, and shall reject bodies that do not parse as a CSR with HTTP 400.
- **Rationale:** Bounded, validated enrollment input protects the gateway from malformed and oversized payloads.
- **Source:** KSPEC-002 UR-016, UR-023; KSPEC-005 ICD-EST items.
- **Classification:** TO-BE (elaborates AS-IS behavior; acceptance of bare `text/plain` CSRs is an AS-IS leniency, not part of this requirement).
- **Priority:** High
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/EstController.cs` (SimpleEnroll, `RequestSizeLimit`, 413 mapping) CODE-OBSERVED; `src/RTSec.Kryptonian.Infrastructure/Crypto/PkcsService.cs` (DecodeEstRequestBodyAsync) CODE-OBSERVED.
- **Approval:** Review Required

### SR-F-EST.004 — Successful enrollment response

- **ID:** SR-F-EST.004
- **Requirement:** The gateway shall respond to a successful enrollment or renewal with HTTP 200 carrying the issued certificate chain as base64 PKCS#7 with content type `application/pkcs7-mime; smime-type=certs-only` and `Content-Transfer-Encoding: base64`.
- **Rationale:** A single response format across CA backends is what allows one device client implementation.
- **Source:** KSPEC-002 UR-016, UR-018.
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** Critical
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/EstController.cs` (HandleEnrollmentResult) CODE-OBSERVED; executed Schannel mTLS renewal using returned certificate VERIFIED per docs/IMPLEMENTATION-TRACKER.md.
- **Approval:** Review Required

### SR-F-EST.005 — Unknown or disabled profile labels fail closed

- **ID:** SR-F-EST.005
- **Requirement:** The gateway shall resolve the EST profile label against configured, enabled profiles only, shall respond HTTP 404 to requests whose label does not match a configured enabled profile, and shall not fall back to a default profile or another label.
- **Rationale:** Silent fallback to a default policy could enroll devices under the wrong trust and issuance policy.
- **Source:** KSPEC-002 UR-022 (unknown-label rejection); PR 37.
- **Classification:** TO-BE (normative statement of AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/EstController.cs` (ResolveProfileIdAsync, 404 "EST profile not found") CODE-OBSERVED; PR 37 profile issuer routing and unknown-label rejection per docs/IMPLEMENTATION-TRACKER.md.
- **Approval:** Review Required

### SR-F-EST.006 — Initial enrollment bootstrap authentication

- **ID:** SR-F-EST.006
- **Requirement:** The gateway shall accept initial enrollment (`simpleenroll`) for a pending device whose CSR identity passes binding (SR-F-REG.005) when the request carries HTTP Basic credentials of the form device-record-Guid : activation code, and shall require HTTPS for such requests, rejecting them with HTTP 403 on plain HTTP.
- **Rationale:** Standard HTTP Basic over TLS is the standards-based bootstrap credential mechanism; requiring TLS protects the one-time secret in transit.
- **Source:** KSPEC-002 UR-002, UR-019; PR 42 (standard Basic EST bootstrap; atomic one-time activation).
- **Classification:** TO-BE (normative statement of AS-IS behavior).
- **Priority:** Critical
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/EstController.cs` (SimpleEnroll Basic parsing, "Basic authentication requires TLS" 403) CODE-OBSERVED; independent curl HTTP-Basic EST over real HTTPS VERIFIED per docs/IMPLEMENTATION-TRACKER.md.
- **Approval:** Review Required

### SR-F-EST.007 — Legacy activation headers as compatibility only

- **ID:** SR-F-EST.007
- **Requirement:** The gateway shall accept the legacy compatibility headers `X-Activation-Code` and `X-Device-*` on `simpleenroll` for backward compatibility with the sample device client only, shall document them as legacy, shall require the same TLS protection and validation rules as the Basic bootstrap path, and shall not require them of any client; the TO-BE system shall provide a configuration switch to disable them on deployments that do not need compatibility.
- **Rationale:** A proprietary-header requirement would recreate vendor lock-in; the headers exist only for the bundled sample client.
- **Source:** KSPEC-002 UR-019 and §16.3 (legacy headers non-normative); EXC-004.
- **Classification:** TO-BE (compatibility retention with a deprecation switch).
- **Priority:** High
- **Status:** Partial
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/EstController.cs` (X-Activation-Code header fallback path) CODE-OBSERVED; docs/IMPLEMENTATION-TRACKER.md (legacy headers "must not become a new interoperability requirement") DOCUMENTED; no disable switch exists CODE-OBSERVED.
- **Approval:** Review Required

### SR-F-EST.008 — Re-enrollment requires native TLS client certificate

- **ID:** SR-F-EST.008
- **Requirement:** The gateway shall require a native TLS client certificate for `simplereenroll` and shall respond HTTP 401 when none is presented, without accepting activation codes or Basic bootstrap credentials in place of the certificate.
- **Rationale:** Renewal trust must rest on possession of the currently valid credential, not on replayable bootstrap secrets.
- **Source:** KSPEC-002 UR-021 (UC-03, SCN-03); PR 44.
- **Classification:** TO-BE (normative statement of AS-IS behavior).
- **Priority:** Critical
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/EstController.cs` (SimpleReenroll, client-certificate 401) CODE-OBSERVED; renewal with native TLS client certificate, new key, preserved identity VERIFIED per docs/IMPLEMENTATION-TRACKER.md.
- **Approval:** Review Required

### SR-F-EST.009 — Client certificate validation on EST requests

- **ID:** SR-F-EST.009
- **Requirement:** The gateway shall validate every presented TLS client certificate on EST requests against the trust policy of the resolved profile (issuer/anchor trust, validity, and purpose), shall additionally require a client certificate on `simpleenroll` when the profile is configured to require one, and shall reject validation failures with HTTP 403.
- **Rationale:** A presented certificate must never be accepted without validation, even where a profile does not mandate one for bootstrap.
- **Source:** KSPEC-002 UR-008, UR-021; PR 45 (explicit EST client trust).
- **Classification:** TO-BE (normative statement of AS-IS behavior).
- **Priority:** Critical
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/EstController.cs` (ValidateClientCertificateAsync, RequireClientCertificate) CODE-OBSERVED; `src/RTSec.Kryptonian.Api/Authentication/ConfiguredClientCertificateTrust.cs` CODE-OBSERVED.
- **Approval:** Review Required

### SR-F-EST.010 — Pending enrollment responses

- **ID:** SR-F-EST.010
- **Requirement:** The gateway shall respond HTTP 202 with a `Retry-After` delay-seconds header and a JSON body when certificate issuance is pending, such that clients can distinguish this outcome from success and from failure without parsing free text.
- **Rationale:** Asynchronous backends need a defined retryable outcome so clients back off rather than hard-fail.
- **Source:** KSPEC-002 UR-023; PR 44; KSPEC-005 ICD-EST.004.
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/EstController.cs` (HandleEnrollmentResult, 202 + Retry-After) CODE-OBSERVED; docs/API-GUIDE.md (Retry-After semantics) DOCUMENTED.
- **Approval:** Review Required

### SR-F-EST.011 — Deterministic pending semantics per backend

- **ID:** SR-F-EST.011
- **Requirement:** The gateway shall define, for every pending-capable CA backend type, the deterministic conditions under which enrollment returns a pending result and the rule by which the `Retry-After` value is computed; the per-backend trigger matrix is `TBD` pending stakeholder agreement (KSPEC-005 open item).
- **Rationale:** Undocumented pending triggers make client retry behavior and diagnostics unpredictable.
- **Source:** KSPEC-005 ICD-EST.004 open item ("which CA backend states produce IsPending — UNKNOWN"); KSPEC-002 UR-023.
- **Classification:** TO-BE (specification gap; AS-IS trigger conditions UNKNOWN).
- **Priority:** Medium
- **Status:** Missing
- **Verification:** A
- **Evidence:** `src/RTSec.Kryptonian.Application/Services/EnrollmentOrchestrator.cs` (EnrollmentResult.IsPending/RetryAfterSeconds) CODE-OBSERVED; docs/formal-specification/05-INTERFACE-CONTROL.md §6 (open item) DOCUMENTED.
- **Approval:** Review Required

### SR-F-EST.012 — Plain-HTTP EST refusal

- **ID:** SR-F-EST.012
- **Requirement:** The gateway shall refuse EST requests received over plain HTTP with HTTP 426 unless the development-only escape `Kryptonian:Est:AllowPlainHttp` is explicitly enabled.
- **Rationale:** EST exchanges carry credentials and issued certificates; plain HTTP exposes both.
- **Source:** KSPEC-002 UR-002 (secret protection); KSPEC-005 ICD-EST (426 extension).
- **Classification:** TO-BE (normative statement of AS-IS behavior).
- **Priority:** Critical
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Program.cs` (EST plain-HTTP 426 middleware, AllowPlainHttp default false) CODE-OBSERVED; `src/RTSec.Kryptonian.Api/appsettings.json` CODE-OBSERVED.
- **Approval:** Review Required

### SR-F-EST.013 — Distinguishable EST/admin error semantics

- **ID:** SR-F-EST.013
- **Requirement:** The gateway shall return distinguishable HTTP status codes for failed device operations — invalid request (400), missing/invalid credential (401), policy-rejected (403), unknown or disabled profile (404), oversized body (413), rate-limited (429), internal error (500) — each with a machine-readable JSON error body on non-PKCS#7 responses.
- **Rationale:** Client developers need actionable failures; undifferentiated errors mask security events and raise support burden.
- **Source:** KSPEC-002 UR-023 (SCN-01/SCN-02).
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** I
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/EstController.cs` (EstError status mapping) CODE-OBSERVED; docs/API-GUIDE.md (status table) DOCUMENTED; docs/medical-device-demo.md (troubleshooting semantics) DOCUMENTED.
- **Approval:** Review Required

### SR-F-EST.014 — CSR attributes guidance endpoint

- **ID:** SR-F-EST.014
- **Requirement:** The gateway shall expose an EST `csrattrs` endpoint describing the CSR attributes (subject DN components and extensions) required by each profile, with bounded per-profile content.
- **Rationale:** Constrained clients otherwise need out-of-band knowledge of profile policy to construct acceptable CSRs.
- **Source:** KSPEC-004 PROPOSED item P-3; RFC 7030 (design input, not a conformance claim).
- **Classification:** PROPOSED (enhancement requiring stakeholder approval).
- **Priority:** Low
- **Status:** Missing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/EstController.cs` (no csrattrs route) CODE-OBSERVED; docs/formal-specification/04-SOFTWARE-DESIGN.md (P-3) DOCUMENTED.
- **Approval:** Review Required

---

## 5. Functional requirements — certificate lifecycle management flows (SR-F-CLM)

### SR-F-CLM.001 — Administrator approval gates enrollment

- **ID:** SR-F-CLM.001
- **Requirement:** The gateway shall issue certificates only to devices whose registration has been approved by an administrator, and shall reject enrollment attempts for devices that are not approved.
- **Rationale:** Approval is the institutional decision that converts a pending registration into an enrollable identity.
- **Source:** KSPEC-002 UR-001, UR-008 (UC-01/UC-02).
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** Critical
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/DevicesController.cs` (Approve, DeviceAdmin policy) CODE-OBSERVED; `src/RTSec.Kryptonian.Application/Services/EnrollmentOrchestrator.cs` (status gate) CODE-OBSERVED.
- **Approval:** Review Required

### SR-F-CLM.002 — Automatic renewal without re-approval

- **ID:** SR-F-CLM.002
- **Requirement:** The gateway shall allow an active, non-revoked device to renew its certificate via `simplereenroll` within the renewal window without re-presenting an activation code or requiring administrator approval.
- **Rationale:** Renewal must be automatic and unattended; re-approval would defeat the automation goal.
- **Source:** KSPEC-002 UR-021, UR-025 (UC-03, SCN-03).
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** Critical
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Application/Services/EnrollmentOrchestrator.cs` (ReenrollAsync) CODE-OBSERVED; renewal executed with new key and preserved identity VERIFIED per docs/IMPLEMENTATION-TRACKER.md.
- **Approval:** Review Required

### SR-F-CLM.003 — Renewal generates a new key and preserves approved identity

- **ID:** SR-F-CLM.003
- **Requirement:** The gateway shall issue renewal certificates against a newly generated device key pair while preserving the approved subject identity from the prior certificate, and shall reject renewal CSRs that do not match the approved identity policy.
- **Rationale:** Fresh keys bound to a stable identity prevent renewal from becoming an identity-substitution channel.
- **Source:** KSPEC-002 UR-021; PR 44/PR 43.
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** Critical
- **Status:** Existing
- **Verification:** T
- **Evidence:** docs/IMPLEMENTATION-TRACKER.md (renewal uses a native TLS client certificate, generates a new key, preserves approved identity) VERIFIED; `src/RTSec.Kryptonian.Application/Services/EnrollmentCertificatePolicy.cs` CODE-OBSERVED.
- **Approval:** Review Required

### SR-F-CLM.004 — Certificate expiry visibility

- **ID:** SR-F-CLM.004
- **Requirement:** The gateway shall expose, for every tracked certificate, its subject, issuer, serial number, status, and NotAfter timestamp to administrators so devices at risk of losing network trust can be identified.
- **Rationale:** Expired credentials cause transport failures that present as device "network problems"; early visibility enables planned maintenance.
- **Source:** KSPEC-002 UR-024 (UC-05/UC-09).
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** D
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/CertificatesController.cs` CODE-OBSERVED; `src/RTSec.Kryptonian.Api/Controllers/DevicesController.cs` (AllCertificates) CODE-OBSERVED; README.md (certificate expiry tracking) DOCUMENTED.
- **Approval:** Review Required

### SR-F-CLM.005 — Automated expiry scanning and notification

- **ID:** SR-F-CLM.005
- **Requirement:** The gateway shall scan daily for device certificates that enter the configured near-expiry warning window, shall send one notification per affected certificate to opted-in recipients, shall skip certificates for which a renewal has already been requested, and shall re-notify about the same certificate no more frequently than the configured re-notification interval.
- **Rationale:** Automated expiry detection is the compensating control that makes unattended renewal safe.
- **Source:** KSPEC-002 UR-012, UR-024 (UC-09); docs/email-notifications.md.
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** High
- **Status:** Partial
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/HostedServices/CertificateExpiryWatcher.cs` (24h scan interval, 7-day re-notify, renewal check) CODE-OBSERVED; `src/RTSec.Kryptonian.Application/Notifications/NotificationDispatcher.cs` CODE-OBSERVED.
- **AS-IS limitation:** Scan interval is fixed at 24 hours, startup delay at 2 minutes, and re-notification at 7 days. Only the warning window is configurable. Renewal suppression tests `Device.LastCertificateId` for a different certificate (successful newer issuance), not whether a renewal was merely requested. Dispatcher success is not proof of SMTP delivery. Configurable re-notification and delivery evidence remain TO-BE (RF-009, VT-029).
- **Approval:** Review Required

### SR-F-CLM.006 — Non-renewal alerting

- **ID:** SR-F-CLM.006
- **Requirement:** The gateway shall notify configured administrators by email when a device certificate approaches expiry and has not been renewed as expected; the complete production trigger set and delivery-reliability mechanism are `TBD` (KSPEC-001 TBD-002).
- **Rationale:** Non-renewal is an early signal of device problems; expiry otherwise leads to silent outages.
- **Source:** KSPEC-002 UR-012 (UC-09); README.md (Renewal monitoring).
- **Classification:** TO-BE (elaborates AS-IS behavior; delivery behavior at baseline UNKNOWN).
- **Priority:** High
- **Status:** Partial
- **Verification:** D
- **Evidence:** `src/RTSec.Kryptonian.Api/HostedServices/CertificateExpiryWatcher.cs` (skips renewed certificates; non-renewal is implied by expiry-window presence) CODE-OBSERVED; `src/RTSec.Kryptonian.Infrastructure/Notifications/MailKitSmtpSender.cs` CODE-OBSERVED; docs/email-notifications.md DOCUMENTED; docs/IMPLEMENTATION-TRACKER.md (delivery not exercised) DOCUMENTED.
- **Approval:** Review Required

### SR-F-CLM.007 — Authorized revocation with issuer-scoped signed CRLs

- **ID:** SR-F-CLM.007
- **Requirement:** The gateway shall support administrative certificate revocation that marks the certificate revoked, denies re-enrollment authenticated with that revoked credential, and publishes an issuer-scoped signed CRL reflecting the revocation.
- **Rationale:** Revocation must separate "deny further enrollment" from "peers reject an already issued certificate," using ordinary PKI mechanisms.
- **Source:** KSPEC-002 UR-010 (UC-04, SCN-05); PR 46.
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** Critical
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Application/Services/CertificateRevocationService.cs` CODE-OBSERVED; `src/RTSec.Kryptonian.Api/Controllers/CrlController.cs` CODE-OBSERVED; administrator revocation followed by native online X509Chain Revoked result VERIFIED per docs/IMPLEMENTATION-TRACKER.md.
- **AS-IS distinction:** This service changes certificate and issuer CRL state; it does not change Device.Status. Other valid credentials for the device are not automatically revoked. Device removal is a separate administrative operation (SR-C.009).
- **Approval:** Review Required

### SR-F-CLM.008 — Transactional revocation persistence

- **ID:** SR-F-CLM.008
- **Requirement:** The gateway shall persist revocation outcome — certificate status, revocation timestamp, and issuer CRL state — atomically, and shall reject a concurrent conflicting update with HTTP 409 rather than leaving inconsistent state.
- **Rationale:** Partially applied revocations would leave credentials in an indeterminate trust state.
- **Source:** KSPEC-002 UR-010; PR 46 (transactional persistence and concurrent-update protection).
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Application/Services/CertificateRevocationService.cs` CODE-OBSERVED; `src/RTSec.Kryptonian.Infrastructure/Data/UnitOfWork.cs` CODE-OBSERVED; docs/formal-specification/07-OPERATIONAL-RISK.md (transactional revocation) DOCUMENTED.
- **Approval:** Review Required

### SR-F-CLM.009 — Revoked devices denied renewal

- **ID:** SR-F-CLM.009
- **Requirement:** The gateway shall refuse `simplereenroll` requests for devices whose current credential is revoked or whose device record is untrusted, and shall record the refusal as a blocked enrollment event.
- **Rationale:** Revocation must terminate the credential lifecycle, not merely mark a status field.
- **Source:** KSPEC-002 UR-009, UR-010.
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** Critical
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Application/Services/EnrollmentOrchestrator.cs` (trust/status gate) CODE-OBSERVED; proxy tests rejecting revoked clients VERIFIED per docs/IMPLEMENTATION-TRACKER.md.
- **Approval:** Review Required

---

## 6. Functional requirements — CA backend integration (SR-F-CAI)

### SR-F-CAI.001 — Multi-backend configuration without code changes

- **ID:** SR-F-CAI.001
- **Requirement:** The gateway shall allow an administrator to configure multiple CA backends through the admin portal using guided fields or an advanced JSON configuration, without code changes.
- **Rationale:** Hospitals use different CA technologies; configuration must not require recompilation or vendor intervention.
- **Source:** KSPEC-002 UR-004 (UC-06); docs/ca-backend-configuration.md.
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** D
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/CaBackendsController.cs` CODE-OBSERVED; `src/RTSec.Kryptonian.Application/Services/CaBackendService.cs` CODE-OBSERVED; docs/ca-backend-configuration.md DOCUMENTED.
- **Approval:** Review Required

### SR-F-CAI.002 — Backend testing and profile-bound issuer routing

- **ID:** SR-F-CAI.002
- **Requirement:** The gateway shall support testing a configured CA backend before activation, shall route each device enrollment to the enabled backend bound to the resolved EST profile, independently of the globally active dashboard backend, and shall not require the device to send a backend identifier.
- **Rationale:** Prevents misconfigured backends from receiving live enrollments and preserves the device-side single-protocol abstraction.
- **Source:** KSPEC-002 UR-005 (UC-06, SCN-06).
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/CaBackendsController.cs` (test/activation endpoints) CODE-OBSERVED; `EnrollmentOrchestrator.ResolveProfileBackendAsync` and `GetCaCertsAsync` CODE-OBSERVED; docs/ca-backend-configuration.md (testing and activation) DOCUMENTED.
- **Approval:** Review Required

### SR-F-CAI.003 — Pluggable connector abstraction

- **ID:** SR-F-CAI.003
- **Requirement:** The gateway shall connect to CA backends through a common internal connector abstraction supporting Self-Signed, ACME (RFC 8555), ADCS (SCEP-compatible service), and EJBCA (REST) backend types, and shall reject unknown backend types rather than defaulting to a backend.
- **Rationale:** The connector abstraction is the mechanism behind CA-agnostic device enrollment and future backend growth.
- **Source:** KSPEC-002 UR-030, UR-038 (UC-06).
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** A
- **Evidence:** `src/RTSec.Kryptonian.Infrastructure/Crypto/CaConnectorFactory.cs` (factory; `NotSupportedException` for unknown types) CODE-OBSERVED; `src/RTSec.Kryptonian.Infrastructure/Harness/AdcsCaConnector.cs`, `EjbcaCaConnector.cs`, `SelfSignedEstCaConnector.cs`; `src/RTSec.Kryptonian.Infrastructure/Acme/AcmeCaConnector.cs` CODE-OBSERVED.
- **Approval:** Review Required

### SR-F-CAI.004 — Transparent CA protocol mapping

- **ID:** SR-F-CAI.004
- **Requirement:** The gateway shall map authenticated device enrollment requests to the hospital-selected CA backend's native protocol such that device-side code requires no CA-specific integration.
- **Rationale:** Preserves the manufacturer's single implementation while hospitals keep CA choice; the gateway is the adaptation layer.
- **Source:** KSPEC-002 UR-018 (UC-06, SCN-06); docs/flow.md (Responsibility Split).
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** Critical
- **Status:** Existing
- **Verification:** A
- **Evidence:** `src/RTSec.Kryptonian.Application/Services/EnrollmentOrchestrator.cs` (backend dispatch) CODE-OBSERVED; docs/flow.md DOCUMENTED.
- **Approval:** Review Required

### SR-F-CAI.005 — ADCS integration via configurable SCEP endpoint

- **ID:** SR-F-CAI.005
- **Requirement:** The gateway shall integrate with an ADCS backend through its SCEP-compatible service using administrator-configured service endpoints, rather than hard-coded endpoint paths.
- **Rationale:** Hard-coded endpoint paths prevent adaptation to real hospital ADCS deployments.
- **Source:** docs/adcs-real-backend-demo.md (Gap 1 — SCEP path is hardcoded); KSPEC-002 UR-030.
- **Classification:** TO-BE (hardening of an AS-IS weak control).
- **Priority:** Medium
- **Status:** Partial
- **Verification:** I
- **Evidence:** `src/RTSec.Kryptonian.Infrastructure/Harness/AdcsCaConnector.cs` and `src/RTSec.Kryptonian.Infrastructure/Harness/ScepClient.cs` (posts to `scep/adcs?operation=…`) CODE-OBSERVED; docs/adcs-real-backend-demo.md Gap 1 DOCUMENTED.
- **Approval:** Review Required

### SR-F-CAI.006 — EJBCA REST integration with explicit revocation configuration

- **ID:** SR-F-CAI.006
- **Requirement:** The gateway shall integrate with an EJBCA backend over its REST interface, shall require the issuer DN to be configured before revocation operations are attempted, and shall report revocation failure honestly (never as success) when the backend is misconfigured or the operation fails.
- **Rationale:** Silent revocation success would mislead operators into trusting revoked credentials.
- **Source:** KSPEC-002 UR-010, UR-030; docs/ca-backend-configuration.md.
- **Classification:** TO-BE (elaborates AS-IS behavior; remote revocation interoperability UNVERIFIED).
- **Priority:** Medium
- **Status:** Partial
- **Verification:** I
- **Evidence:** `src/RTSec.Kryptonian.Infrastructure/Harness/EjbcaCaConnector.cs` (revoke requires `IssuerDn`; returns false on failure/misconfiguration) CODE-OBSERVED; docs/IMPLEMENTATION-TRACKER.md (ADCS/EJBCA hospital revocation unverified) DOCUMENTED.
- **Approval:** Review Required

### SR-F-CAI.007 — ACME enrollment with functional challenge set

- **ID:** SR-F-CAI.007
- **Requirement:** The gateway shall integrate with an ACME (RFC 8555) backend including external account binding, and shall implement each configured challenge type it advertises; a challenge type that is not implemented shall be rejected at configuration time rather than failing at issuance.
- **Rationale:** Advertising a challenge type that is a stub causes unpredictable enrollment failures.
- **Source:** KSPEC-002 UR-030; KSPEC-004 AS-IS limitation (Dns01ChallengeStub).
- **Classification:** TO-BE (hardening of an AS-IS weak control; DNS-01 is a stub at baseline).
- **Priority:** Medium
- **Status:** Partial
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Infrastructure/Acme/AcmeCaConnector.cs`, `Http01ChallengeStore.cs`, `Http01ChallengeCleanupService.cs` CODE-OBSERVED; `src/RTSec.Kryptonian.Infrastructure/Acme/Dns01ChallengeStub.cs` CODE-OBSERVED.
- **Approval:** Review Required

### SR-F-CAI.008 — Gateway CRL generation limited to capable backends; fail closed otherwise

- **ID:** SR-F-CAI.008
- **Requirement:** The gateway shall generate and publish issuer-scoped CRLs for backends whose issuing capability is gateway-held (Self-Signed), shall serve CRLs from configured remote backends when those backends provide revocation data, and shall refuse CRL publication requests for backends without such capability with an explicit not-implemented error rather than publishing a false CRL.
- **Rationale:** A false CRL would misstate revocation trust for all peers of that issuer.
- **Source:** KSPEC-002 UR-010; PR 46; docs/IMPLEMENTATION-TRACKER.md (local CA CRLs demonstrated; unsupported remote CA revocation fails closed).
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Application/Services/CertificateRevocationService.cs` and `src/RTSec.Kryptonian.Api/Controllers/CrlController.cs` (501 on unsupported CRL generation) CODE-OBSERVED; docs/formal-specification/07-OPERATIONAL-RISK.md (fail-closed orientation) DOCUMENTED.
- **Approval:** Review Required

### SR-F-CAI.009 — CA material persistence across restarts and upgrades

- **ID:** SR-F-CAI.009
- **Requirement:** The gateway shall persist configured CA backend material (including CA certificates, connector credentials, and trust anchors) such that a gateway restart or in-place upgrade preserves the configuration and enrollment continues without administrator rework.
- **Rationale:** Loss of CA configuration would silently break enrollment and trust validation for the whole estate.
- **Source:** KSPEC-002 UR-037; PR 41; installer upgrade behavior in docs/formal-specification/07-OPERATIONAL-RISK.md.
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** T
- **Evidence:** docs/IMPLEMENTATION-TRACKER.md (gateway restart on populated SQLite preserved configured profile, issuer and devices) VERIFIED; `src/RTSec.Kryptonian.Infrastructure/Repositories/CaBackendRepository.cs` CODE-OBSERVED.
- **Approval:** Review Required

### SR-F-CAI.010 — Secure connector credential handling

- **ID:** SR-F-CAI.010
- **Requirement:** The gateway shall obtain CA service-account secrets, ACME account keys, and CA key passwords from a secure secrets mechanism (secret vault or OS-protected secret service) rather than plain configuration files; the selected mechanism is `TBD` pending stakeholder decision (TBD-005).
- **Rationale:** Leakage of CA-issuing credentials compromises the institution's entire PKI.
- **Source:** KSPEC-002 UR-013 (Missing); Mvp.md §8.
- **Classification:** TO-BE (not implemented at baseline).
- **Priority:** High
- **Status:** Missing
- **Verification:** I
- **Evidence:** `src/RTSec.Kryptonian.Api/appsettings.json` and `src/RTSec.Kryptonian.Api/Program.cs` (configuration/environment sourcing) CODE-OBSERVED; docs/IMPLEMENTATION-TRACKER.md (environment variables and configuration files at baseline) DOCUMENTED.
- **Approval:** Review Required

---

## 7. Functional requirements — administration and portal (SR-F-ADM)

### SR-F-ADM.001 — Administrative portal surface

- **ID:** SR-F-ADM.001
- **Requirement:** The gateway shall provide an administrator portal covering device registration and lifecycle operations, CA backend configuration, EST profile management, user administration, notification configuration, and audit review.
- **Rationale:** The portal is the primary operating surface for hospital IT staff; coverage gaps force unsafe out-of-band administration.
- **Source:** KSPEC-002 UR-001, UR-003, UR-004, UR-015, UR-024; README.md.
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** D
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/` (15 controllers) CODE-OBSERVED; `src/RTSec.Kryptonian.Web` (Blazor Server) and `src/RTSec.Kryptonian.Ui` (React/TypeScript) CODE-OBSERVED; README.md DOCUMENTED.
- **Approval:** Review Required

### SR-F-ADM.002 — Gateway dashboard summary

- **ID:** SR-F-ADM.002
- **Requirement:** The gateway shall provide a dashboard summarizing CA backend state, registered device counts, enrollment outcomes, and backend evidence.
- **Rationale:** Administrators need rapid assessment of gateway health without querying each subsystem.
- **Source:** KSPEC-002 UR-006.
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** Medium
- **Status:** Existing
- **Verification:** D
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/StatusController.cs` CODE-OBSERVED; README.md (Gateway dashboard) DOCUMENTED.
- **Approval:** Review Required

### SR-F-ADM.003 — User account lifecycle with lockout protection

- **ID:** SR-F-ADM.003
- **Requirement:** The gateway shall allow a SystemAdmin to create, edit, deactivate, and delete user accounts, shall forbid a user from deleting their own account, and shall block deletion of the last active SystemAdmin account.
- **Rationale:** Prevents self-lockout and loss of administrative access to the gateway.
- **Source:** KSPEC-002 UR-007, UR-015; docs/user-management.md (Managing Users; delete protections).
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/UsersController.cs` (SystemAdmin policy) CODE-OBSERVED; `src/RTSec.Kryptonian.Application/Services/UserService.cs` (self-deletion and last-admin guards) CODE-OBSERVED; docs/user-management.md DOCUMENTED.
- **Approval:** Review Required

### SR-F-ADM.004 — API key lifecycle

- **ID:** SR-F-ADM.004
- **Requirement:** The gateway shall allow DeviceAdmin-and-above users to generate, list, and revoke API keys, shall store only a hash of each key, shall support optional key expiry, and shall scope key visibility so a DeviceAdmin sees only their own keys while a SystemAdmin sees all keys with owner attribution.
- **Rationale:** Keys are bearer credentials; hashed storage, scoped visibility, and revocation limit blast radius.
- **Source:** KSPEC-002 UR-027; docs/user-management.md (API Keys).
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/ApiKeysController.cs` (DeviceAdmin policy, UnauthorizedAccessException scoping) CODE-OBSERVED; `src/RTSec.Kryptonian.Infrastructure/Repositories/ApiKeyRepository.cs` CODE-OBSERVED; docs/user-management.md DOCUMENTED.
- **Approval:** Review Required

### SR-F-ADM.005 — Initial administrator bootstrap

- **ID:** SR-F-ADM.005
- **Requirement:** The gateway shall support first-administrator setup through a UI setup wizard and, optionally, through initial-admin environment variables, and shall seed an initial administrator only when gateway setup is still pending.
- **Rationale:** Automated deployments need unattended bootstrap; re-seeding on populated systems would be an account-injection vector.
- **Source:** KSPEC-002 UR-007, UR-015; docs/admin-setup.md; docs/user-management.md (FAQ).
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** T
- **Evidence:** `AuthService.BootstrapAsync`, `AuthController.Setup` and `Program.cs` initial-admin seeding CODE-OBSERVED.
- **Approval:** Review Required

### SR-F-ADM.006 — Notification settings management

- **ID:** SR-F-ADM.006
- **Requirement:** The gateway shall allow SystemAdmin configuration of notification settings (SMTP transport parameters, recipients, event subscriptions including near-expiry and revocation events) and shall provide a test-notification action.
- **Rationale:** Notification routing is deployment-specific (on-prem SMTP relays) and must be verifiable without generating a real incident.
- **Source:** KSPEC-002 UR-012; docs/email-notifications.md.
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** Medium
- **Status:** Existing
- **Verification:** D
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/NotificationSettingsController.cs` (SystemAdmin policies, test endpoint) CODE-OBSERVED; `src/RTSec.Kryptonian.Application/Services/NotificationSettingsService.cs` CODE-OBSERVED; docs/email-notifications.md DOCUMENTED.
- **Approval:** Review Required

### SR-F-ADM.007 — EST profile management with additive trust-list updates

- **ID:** SR-F-ADM.007
- **Requirement:** The gateway shall allow SystemAdmin creation, modification, enable/disable, and deletion of named EST profiles, and shall apply profile updates such that a partial update that omits trust-list fields preserves the previously stored trust lists rather than clearing them.
- **Rationale:** Silent narrowing of trust policy on update would break enrolled devices unpredictably.
- **Source:** KSPEC-002 UR-031, UR-032; PR 47 (partial updates preserve omitted trust lists).
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/EstProfilesController.cs` (SystemAdmin policies) CODE-OBSERVED; `src/RTSec.Kryptonian.Application/Services/EstProfileService.cs` CODE-OBSERVED; `src/RTSec.Kryptonian.Infrastructure/Data/SchemaUpgrades.cs` (EnsureTrustPolicyAsync) CODE-OBSERVED.
- **Approval:** Review Required

### SR-F-ADM.008 — Gateway settings management

- **ID:** SR-F-ADM.008
- **Requirement:** The gateway shall allow SystemAdmin inspection and modification of gateway operational settings persisted in the database, overriding compiled or file defaults without redeployment.
- **Rationale:** Operational tuning (e.g., warning windows) must not require redeployment.
- **Source:** KSPEC-002 UR-006, UR-024; `GatewaySettingsController` surface.
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** Medium
- **Status:** Existing
- **Verification:** I
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/GatewaySettingsController.cs` (SystemAdmin policy) CODE-OBSERVED; `src/RTSec.Kryptonian.Application/Services/GatewaySettingsService.cs` CODE-OBSERVED; `src/RTSec.Kryptonian.Infrastructure/Repositories/GatewaySettingsRepository.cs` CODE-OBSERVED.
- **Approval:** Review Required

### SR-F-ADM.009 — Live activity feed

- **ID:** SR-F-ADM.009
- **Requirement:** The gateway shall provide administrators a live activity view of recent log events through an in-memory ring buffer endpoint, without requiring access to server log files.
- **Rationale:** Operational triage needs real-time visibility from the portal.
- **Source:** KSPEC-002 UR-006, UR-011; `LogsController`/`LiveLogBroadcaster` surface.
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** Medium
- **Status:** Existing
- **Verification:** D
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/LogsController.cs` CODE-OBSERVED; `src/RTSec.Kryptonian.Api/Logging/LiveLogBroadcaster` sink registration in `src/RTSec.Kryptonian.Api/Program.cs` CODE-OBSERVED.
- **Approval:** Review Required

### SR-F-ADM.010 — Health and status endpoint

- **ID:** SR-F-ADM.010
- **Requirement:** The gateway shall expose an unauthenticated health-check endpoint reporting overall health and the health of its database component, suitable for liveness/readiness probes.
- **Rationale:** Monitoring systems and orchestrators need a liveness/readiness probe.
- **Source:** KSPEC-002 UR-029 (SCN-09 context); docs/DEPLOYMENT.md.
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** Medium
- **Status:** Existing
- **Verification:** T
- **Evidence:** `StatusController.cs` and `Program.cs` MapHealthChecks/AddDbContextCheck CODE-OBSERVED; component readiness beyond database UNKNOWN.
- **Approval:** Review Required

## 8. Functional requirements — interoperability and contract artifacts (SR-F-INT)

### SR-F-INT.001 — Independent-client usability of the EST path

- **ID:** SR-F-INT.001
- **Requirement:** The gateway's EST path shall be usable by independent clients that implement standard HTTP authentication and RFC 7030-style message formats only, without requiring gateway-specific headers, user agents, or prior gateway registration beyond the documented bootstrap credential.
- **Rationale:** A proprietary-client requirement would recreate the vendor lock-in the gateway exists to remove.
- **Source:** KSPEC-002 UR-019 (VERIFIED executed evidence); KSPEC-001 EXC-004.
- **Classification:** TO-BE (normative statement of AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** T
- **Evidence:** docs/IMPLEMENTATION-TRACKER.md (independent curl HTTP Basic EST over real HTTPS) VERIFIED; docs/API-GUIDE.md (EstClient example) DOCUMENTED.
- **Approval:** Review Required

### SR-F-INT.002 — Compatibility headers never mandatory

- **ID:** SR-F-INT.002
- **Requirement:** No gateway-specific compatibility header shall be mandatory for enrollment or renewal by any client, and no requirement in any package document shall promote the legacy activation headers to an interoperability requirement.
- **Rationale:** Locks in the single-protocol interoperability proposition; prevents specification drift toward private headers.
- **Source:** KSPEC-002 UR-019 and §16.3; docs/IMPLEMENTATION-TRACKER.md (deliberate boundaries).
- **Classification:** TO-BE (governing constraint).
- **Priority:** High
- **Status:** Existing
- **Verification:** I
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/EstController.cs` (Basic path independent of legacy headers) CODE-OBSERVED; docs/IMPLEMENTATION-TRACKER.md DOCUMENTED.
- **Approval:** Review Required

### SR-F-INT.003 — Authoritative machine-readable admin API contract

- **ID:** SR-F-INT.003
- **Requirement:** The gateway's admin API shall have an authoritative, current, machine-readable OpenAPI contract that describes every implemented endpoint, authentication scheme, and error response, and each contract file in the repository shall either be current and complete for the endpoints it claims or be explicitly marked superseded.
- **Rationale:** Integrators automate against the contract; an empty or stale contract silently misrepresents the API.
- **Source:** KSPEC-002 UR-027; baseline finding: `OpenAPI.Client.yaml` is empty and `OpenAPI.Schema.yaml` is stale (KSPEC-005).
- **Classification:** TO-BE (not satisfied at baseline).
- **Priority:** High
- **Status:** Missing
- **Verification:** I
- **Evidence:** `OpenAPI.Client.yaml` (empty) and `OpenAPI.Schema.yaml` (stale) CODE-OBSERVED; `OpenAPI.Admin.yaml`, `OpenAPI.ESTProfiles.yaml`, `OpenAPI.StatusObserve.yaml` CODE-OBSERVED; docs/formal-specification/05-INTERFACE-CONTROL.md DOCUMENTED.
- **Approval:** Review Required

### SR-F-INT.004 — Contract-driven EST profile schema currency

- **ID:** SR-F-INT.004
- **Requirement:** The EST profile OpenAPI contract shall describe the current EST profile policy fields (subject-DN rules, key usage, validity, trust policy, client-certificate requirement, enablement) as actually enforced by the gateway.
- **Rationale:** Profile policy is the security boundary between the institution and devices; contract drift hides enforcement changes.
- **Source:** KSPEC-002 UR-031; `OpenAPI.ESTProfiles.yaml` review.
- **Classification:** TO-BE (elaborates AS-IS behavior; requires contract-vs-implementation reconciliation).
- **Priority:** Medium
- **Status:** Partial
- **Verification:** I
- **Evidence:** `OpenAPI.ESTProfiles.yaml` CODE-OBSERVED; `src/RTSec.Kryptonian.Application/DTOs/EstProfileDto.cs` CODE-OBSERVED; docs/formal-specification/05-INTERFACE-CONTROL.md DOCUMENTED.
- **Approval:** Review Required

### SR-F-INT.005 — DICOM mTLS demonstration over gateway-issued credentials

- **ID:** SR-F-INT.005
- **Requirement:** The repository shall include a demonstration in which two independent device peers establish a mutually authenticated TLS DICOM connection using gateway-issued certificates, with both peers enforcing issuer trust, certificate purpose, and online revocation status.
- **Rationale:** End-to-end lifecycle evidence (issuance → use → revocation) is the credibility core of the research contribution.
- **Source:** KSPEC-002 UR-034, UR-036 (research goals); KSPEC-000 §3 (demonstration clients are not product deliverables).
- **Classification:** AS-IS (demonstration component; not a product deliverable for production).
- **Priority:** Medium
- **Status:** Existing
- **Verification:** D
- **Evidence:** `src/Kryptonian.DICOMTls/` CODE-OBSERVED; two mutual-TLS DICOM C-STORE transfers with SOP Instance UID comparison VERIFIED per docs/IMPLEMENTATION-TRACKER.md and `artifacts/developer/verify-*/evidence.json`; tests/RTSec.Kryptonian.DimseTlsProxy.Tests/ CODE-OBSERVED.
- **Approval:** Review Required

### SR-F-INT.006 — Demonstration components carry no production or clinical claims

- **ID:** SR-F-INT.006
- **Requirement:** The gateway's demonstration components (device simulator, DIMSE-TLS proxy) shall be documented as reference/demonstration clients only, shall not be presented as production deliverables, and shall not be described as conferring clinical capability, SDC participation, or regulatory status on any device.
- **Rationale:** Preserves the identity/capability boundary (KSPEC-000 §3) and prevents overstating maturity.
- **Source:** KSPEC-002 UR-026, UR-035; README.md disclaimer.
- **Classification:** TO-BE (governing constraint).
- **Priority:** High
- **Status:** Existing
- **Verification:** I
- **Evidence:** `src/Kryptonian.MedicalDevice/`, `src/Kryptonian.DICOMTls/` CODE-OBSERVED; README.md disclaimer and docs/IMPLEMENTATION-TRACKER.md (deliberate boundaries) DOCUMENTED.
- **Approval:** Review Required

### SR-F-INT.007 — No competing enrollment protocol

- **ID:** SR-F-INT.007
- **Requirement:** The gateway shall not define or require a second proprietary enrollment protocol; its contribution to the OR.NET / IEEE 11073 SDC enrollment effort shall be operational evidence aligned with EST-based direction.
- **Rationale:** A competing protocol would fragment the enrollment landscape the project aims to support.
- **Source:** KSPEC-002 UR-034; docs/ORNET-ALIGNMENT-2026-09-09.md; docs/CHANGES-AND-ORNET-ALIGNMENT.md.
- **Classification:** TO-BE (governing constraint).
- **Priority:** High
- **Status:** Existing
- **Verification:** I
- **Evidence:** README.md; docs/IMPLEMENTATION-TRACKER.md (deliberate boundaries: no SDC conformity claimed); docs/ORNET-ALIGNMENT-2026-09-09.md DOCUMENTED.
- **Approval:** Review Required

### SR-F-INT.008 — Joint interoperability test vectors

- **ID:** SR-F-INT.008
- **Requirement:** The project shall maintain an interoperability testing approach in which independent client/server pairs exercise the real network boundary (enrollment authentication, issuer trust, certificate identity/purposes, renewal, revocation timing, failure behavior), with test vectors agreed jointly with OR.NET where possible.
- **Rationale:** Controller-mocked tests missed real defects at baseline; boundary-level testing is the agreed path to credible evidence.
- **Source:** KSPEC-002 UR-036; docs/IMPLEMENTATION-TRACKER.md ("live checks exposed two bugs that mocked tests missed").
- **Classification:** PROPOSED (joint vectors require stakeholder agreement; boundary testing itself is AS-IS).
- **Priority:** Medium
- **Status:** Proposed
- **Verification:** T
- **Evidence:** `artifacts/developer/verify-*/evidence.json` (gateway-boundary verifier) VERIFIED; tests/ (8 test projects) CODE-OBSERVED; OR.NET joint vectors not present PROPOSED.
- **Approval:** Review Required

---

## 9. Security requirements (SR-S)

### SR-S.001 — Transport security for credential-bearing exchanges

- **ID:** SR-S.001
- **Requirement:** The gateway shall require TLS for EST endpoints and for any request carrying authentication credentials, shall enable HTTPS redirection outside Development, and shall refuse unencrypted EST traffic with HTTP 426.
- **Rationale:** Bootstrap secrets, CSRs, and issued certificates in transit are the core confidentiality/identity assets of the system.
- **Source:** KSPEC-002 UR-002, UR-014; docs/ARCHITECTURE.md (Security by Default).
- **Classification:** TO-BE (normative statement of AS-IS behavior).
- **Priority:** Critical
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Program.cs` (426 middleware, `Kryptonian:Tls:RedirectHttpToHttps` default enabled outside Development) CODE-OBSERVED; docs/DEPLOYMENT.md DOCUMENTED.
- **Approval:** Review Required

### SR-S.002 — CSR and returned-certificate policy enforcement

- **ID:** SR-S.002
- **Requirement:** The gateway shall evaluate the submitted CSR and the CA-returned certificate against the resolved EST profile's issuance policy (subject DN rules, SAN, EKU, key usage, validity) and shall refuse to deliver any credential whose identity or purpose exceeds the approved policy.
- **Rationale:** A malicious or faulty client must not obtain credentials whose identity or purpose exceeds its approved policy.
- **Source:** KSPEC-002 UR-033 (Critical, VERIFIED); PR 43.
- **Classification:** TO-BE (normative statement of AS-IS behavior).
- **Priority:** Critical
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Application/Services/EnrollmentCertificatePolicy.cs` CODE-OBSERVED; PR 43 CSR and returned-certificate policy enforcement VERIFIED per docs/IMPLEMENTATION-TRACKER.md.
- **Approval:** Review Required

### SR-S.003 — Gateway-owned trust-policy gate

- **ID:** SR-S.003
- **Requirement:** The gateway shall evaluate device trust policy itself and shall reject certificate requests from unknown, unactivated, untrusted, or retired devices before any contact with the CA backend.
- **Rationale:** The gateway, not the CA, owns the medical-device trust decision; CA-side rejection is not a substitute.
- **Source:** KSPEC-002 UR-008 (Critical); docs/flow.md (Trust Policy Role).
- **Classification:** TO-BE (normative statement of AS-IS behavior).
- **Priority:** Critical
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Application/Services/EnrollmentOrchestrator.cs` (pre-CA trust gate) CODE-OBSERVED; docs/flow.md DOCUMENTED.
- **Approval:** Review Required

### SR-S.004 — Forwarded-certificate identity guard

- **ID:** SR-S.004
- **Requirement:** The gateway shall accept a client TLS identity supplied via the `X-Forwarded-Client-Cert` header only from explicitly configured trusted proxies, shall reject such a header from any other source with HTTP 403, shall reject it when a native client certificate is also present, and shall reject multiple forwarding headers with HTTP 400.
- **Rationale:** Uncontrolled certificate forwarding allows any client to assert a forged peer identity.
- **Source:** PR 39 (trusted forwarding guard); KSPEC-005 ICD (CertificateForwardingGuard); KSPEC-002 UR-008.
- **Classification:** TO-BE (normative statement of AS-IS behavior).
- **Priority:** Critical
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Middleware/CertificateForwardingGuard.cs` (KnownProxies allow-list, 403/400 behavior, default empty = fail closed) CODE-OBSERVED; direct forwarded-certificate spoof rejected VERIFIED per docs/IMPLEMENTATION-TRACKER.md.
- **Approval:** Review Required

### SR-S.005 — Authenticated forwarder identity

- **ID:** SR-S.005
- **Requirement:** The gateway shall authenticate the forwarding proxy itself for the `X-Forwarded-Client-Cert` path (by mutual TLS, shared secret, or equivalent cryptographic means) and shall not rely solely on the connection source IP address to authorize certificate forwarding; the selected mechanism is `TBD` pending design approval.
- **Rationale:** A source-IP allow-list alone can be spoofed on routed networks; the forwarded identity is as sensitive as the credential itself.
- **Source:** KSPEC-004 PROPOSED item P-2; KSPEC-006 threat model (XFCC spoofing route).
- **Classification:** TO-BE (hardening of an AS-IS weak control).
- **Priority:** High
- **Status:** Missing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Middleware/CertificateForwardingGuard.cs` (IP-set membership check only) CODE-OBSERVED; docs/formal-specification/04-SOFTWARE-DESIGN.md P-2 ("header trusted unconditionally at baseline") DOCUMENTED.
- **Approval:** Review Required

### SR-S.006 — EST rate limiting

- **ID:** SR-S.006
- **Requirement:** The gateway shall rate-limit device-facing EST endpoints per client IP by default, shall return HTTP 429 when the limit is exceeded, and shall make the limits configurable; the production limits are `TBD` (recommendation: retain the configured 100 requests/minute on `*:/.well-known/est/*` as the development default and set production values with OR.NET).
- **Rationale:** Device-facing endpoints are exposed to the hospital network and must tolerate abusive traffic.
- **Source:** KSPEC-002 UR-014 (Medium); docs/ARCHITECTURE.md (rate limiting).
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** Medium
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Program.cs` (UseIpRateLimiting, AspNetCoreRateLimit registration) CODE-OBSERVED; `src/RTSec.Kryptonian.Api/appsettings.json` (IpRateLimiting rules) CODE-OBSERVED; docs/ARCHITECTURE.md DOCUMENTED.
- **AS-IS detail:** Specific rules additionally specify 30 requests/minute for `POST:/.well-known/est/*/simpleenroll` and `POST:/.well-known/est/*/simplereenroll`. Effective matching on unlabeled paths has not been demonstrated by this review. Configuration is `IpRateLimiting`, not the similarly named `Kryptonian:RateLimiting` section.
- **Approval:** Review Required

### SR-S.007 — No hard-coded signing secrets in production

- **ID:** SR-S.007
- **Requirement:** The gateway shall obtain the JWT signing secret from configuration in every environment, shall fail fast at startup in Production when it is absent or set to a known development default, and shall never embed a hard-coded secret value in the application.
- **Rationale:** A hard-coded fallback signing key allows any party with the source to forge administrator tokens.
- **Source:** Baseline finding: `Program.cs` falls back to the hard-coded string `kryptonian-dev-secret-not-for-production-32chars` (KSPEC-005 finding 5); KSPEC-002 UR-013.
- **Classification:** TO-BE (hardening of an AS-IS weak control; the hard-coded fallback must be removed).
- **Priority:** Critical
- **Status:** Missing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Program.cs` (jwtSecret fallback constant, lines ~177-183) CODE-OBSERVED; docs/formal-specification/05-INTERFACE-CONTROL.md (ICD-CFG.002 finding) DOCUMENTED.
- **Approval:** Review Required

### SR-S.008 — Secrets at rest

- **ID:** SR-S.008
- **Requirement:** The gateway shall encrypt at rest all sensitive credential fields it persists (connector credentials, SMTP passwords, CA key passwords), using a key-protected mechanism; the encryption/key-ring approach is `TBD` pending platform decision.
- **Rationale:** Database-level access must not expose institutional PKI credentials in plaintext.
- **Source:** KSPEC-002 UR-013 (Missing); KSPEC-004 data-design context.
- **Classification:** TO-BE (not implemented at baseline for most credential fields).
- **Priority:** High
- **Status:** Missing
- **Verification:** I
- **Evidence:** `src/RTSec.Kryptonian.Infrastructure/Data/KryptonianDbContext.cs` CODE-OBSERVED; `src/RTSec.Kryptonian.Infrastructure/Security/DataProtectionService.cs` CODE-OBSERVED (protection service exists but field coverage not evidenced); docs/IMPLEMENTATION-TRACKER.md (environment/plain-config storage) DOCUMENTED.
- **Approval:** Review Required

### SR-S.009 — Password hashing

- **ID:** SR-S.009
- **Requirement:** The gateway shall store administrator password verifications only as salted adaptive hashes (BCrypt or an equivalent work-factor algorithm, cost factor ≥ 12) and shall never store plaintext passwords.
- **Rationale:** Credential database exposure must not yield usable passwords.
- **Source:** KSPEC-002 UR-015; plans/user-management.md (BCrypt cost 12).
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** Critical
- **Status:** Existing
- **Verification:** I
- **Evidence:** BCrypt.Net-Next dependency in `src/RTSec.Kryptonian.Api/RTSec.Kryptonian.Api.csproj` CODE-OBSERVED; `src/RTSec.Kryptonian.Application/Services/AuthService.cs` and `UserService.cs` CODE-OBSERVED; plans/user-management.md DOCUMENTED.
- **Approval:** Review Required

### SR-S.010 — Data Protection key ring management

- **ID:** SR-S.010
- **Requirement:** The gateway shall configure the ASP.NET Core Data Protection key ring for persistent, deployment-appropriate storage in every deployment mode, and shall fail closed (refuse to start or degrade explicitly) where persisted key state is incomplete.
- **Rationale:** Ephemeral or inaccessible key rings silently invalidate protected data across restarts.
- **Source:** Sandbox behavior (keys persisted to data directory); docs/windows-installation.md (incomplete key state deliberately fails closed).
- **Classification:** TO-BE (elaborates AS-IS behavior; non-sandbox production key-ring persistence policy not evidenced — UNKNOWN).
- **Priority:** High
- **Status:** Partial
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Program.cs` (AddDataProtection, PersistKeysToFileSystem for sandbox) CODE-OBSERVED; `src/RTSec.Kryptonian.Infrastructure/Security/DataProtectionService.cs` CODE-OBSERVED; artifacts/windows-installer/payload/docs/windows-installation.md DOCUMENTED.
- **Approval:** Review Required

### SR-S.011 — Device private key non-archival

- **ID:** SR-S.011
- **Requirement:** The gateway shall not archive device private keys, and any legacy schema field capable of holding an archived private key shall either be removed or restricted to explicitly approved server-side-key-generation profiles; the policy decision is `TBD` pending stakeholder approval.
- **Rationale:** Gateway-held device private keys would allow credential impersonation if the gateway is compromised.
- **Source:** KSPEC-004 data-design finding A-2 (EncryptedPrivateKeyPem field, policy undefined); KSPEC-002 UR-013 rationale.
- **Classification:** PROPOSED (policy decision requiring approval).
- **Priority:** High
- **Status:** Proposed
- **Verification:** I
- **Evidence:** `src/RTSec.Kryptonian.Domain/Entities/` (Certificate entity `EncryptedPrivateKeyPem` field) CODE-OBSERVED; docs/formal-specification/04-SOFTWARE-DESIGN.md A-2 DOCUMENTED.
- **Approval:** Review Required

### SR-S.012 — Configurable CRL lifetime

- **ID:** SR-S.012
- **Requirement:** The gateway shall derive the CRL lifetime and next-update time for gateway-generated CRLs from configurable policy chosen for outage tolerance and revocation objectives, rather than a fixed demonstration value.
- **Rationale:** The 60-second prototype CRL lifetime encodes demo assumptions, not institutional revocation policy.
- **Source:** KSPEC-004 AS-IS finding A-4 (hard-coded 60-second CRL lifetime); KSPEC-002 UR-010.
- **Classification:** TO-BE (hardening of an AS-IS weak control).
- **Priority:** High
- **Status:** Missing
- **Verification:** T
- **Evidence:** Self-signed connector/CRL generation (60 s lifetime) CODE-OBSERVED in `src/RTSec.Kryptonian.Infrastructure/Crypto/SelfSignedCaConnector.cs` and CRL issuance path; docs/formal-specification/04-SOFTWARE-DESIGN.md A-4 DOCUMENTED.
- **Approval:** Review Required

### SR-S.013 — CORS restriction for the admin UI

- **ID:** SR-S.013
- **Requirement:** The gateway shall restrict cross-origin browser access to explicitly configured origins and shall deny all other origins.
- **Rationale:** Unrestricted CORS would expose authenticated admin sessions to malicious sites.
- **Source:** docs/ARCHITECTURE.md (CORS); KSPEC-002 UR-015.
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** Medium
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Program.cs` (KryptonianUi CORS policy from `Kryptonian:Ui:AllowedOrigins`) CODE-OBSERVED; `src/RTSec.Kryptonian.Api/appsettings.json` CODE-OBSERVED.
- **Approval:** Review Required

### SR-S.014 — Dependency vulnerability management

- **ID:** SR-S.014
- **Requirement:** The project's continuous integration shall fail on high or critical advisories in UI dependencies, and the project shall track and disclose remaining transitive advisories as deployment blockers until resolved by a tested update.
- **Rationale:** Known-vulnerable dependencies (SQLitePCLRaw.lib.e_sqlite3 2.1.6; System.Security.Cryptography.Xml 8.0.2 at baseline) are deployment blockers requiring a tested update, and audit disclosure must remain honest.
- **Source:** PR 52 (dependency security updates and audit disclosure); docs/IMPLEMENTATION-TRACKER.md (remaining high advisories).
- **Classification:** TO-BE (elaborates AS-IS behavior; whole-system audit still outstanding).
- **Priority:** High
- **Status:** Partial
- **Verification:** T
- **Evidence:** `.github/workflows/` (CI advisory gate) CODE-OBSERVED; docs/IMPLEMENTATION-TRACKER.md (npm audit reduced to zero; remaining transitive high advisories) DOCUMENTED.
- **Approval:** Review Required

---

## 10. Authentication requirements (SR-A)

### SR-A.001 — Dual-scheme admin authentication

- **ID:** SR-A.001
- **Requirement:** The gateway shall authenticate administrative API requests using either JWT bearer tokens (browser sessions) or the `X-API-Key` header (scripted clients), selected per request, and shall reject unauthenticated administrative requests with HTTP 401.
- **Rationale:** Browser and automation clients have different operational needs; both must pass the same authorization layer.
- **Source:** KSPEC-002 UR-015, UR-027; docs/API-GUIDE.md.
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** Critical
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Program.cs` (PolicyScheme forwarding, JWT + ApiKey schemes) CODE-OBSERVED; `src/RTSec.Kryptonian.Api/Authentication/ApiKeyAuthenticationHandler.cs` CODE-OBSERVED.
- **Approval:** Review Required

### SR-A.002 — JWT token validation

- **ID:** SR-A.002
- **Requirement:** The gateway shall validate JWT issuer, audience, lifetime, and signing key on every bearer-authenticated request, shall use a session lifetime configurable with a default of 8 hours and a 5-minute clock skew, and shall reject invalid or expired tokens.
- **Rationale:** Token validation parameters are the session-security boundary for the admin UI.
- **Source:** docs/user-management.md (8-hour sessions); `JwtLifetimeHours` configuration.
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** Critical
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Program.cs` (TokenValidationParameters, ClockSkew) CODE-OBSERVED; docs/user-management.md DOCUMENTED; 8-hour default DOCUMENTED per docs/formal-specification/05-INTERFACE-CONTROL.md.
- **Approval:** Review Required

### SR-A.003 — API key storage and validation

- **ID:** SR-A.003
- **Requirement:** The gateway shall validate database-stored API keys by one-way hash comparison, shall accept a key only when it is unrevoked, unexpired, and owned by an active user, and shall update last-used information on successful use.
- **Rationale:** Bearer keys must be revocable and bound to active accounts.
- **Source:** KSPEC-002 UR-027; docs/user-management.md.
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** Critical
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Authentication/ApiKeyAuthenticationHandler.cs` (SHA-256 hash lookup, RevokedAt/ExpiresAt/IsActive checks, LastUsedAt update) CODE-OBSERVED.
- **Approval:** Review Required

### SR-A.004 — Static compatibility API keys

- **ID:** SR-A.004
- **Requirement:** The gateway shall continue to accept statically configured API keys (`KRYPTONIAN__ADMINAPI__APIKEYS`) as SystemAdmin for backward compatibility, shall treat them as a legacy mechanism, and shall document migration toward user-generated keys.
- **Rationale:** Existing automation must keep working during migration to per-user keys.
- **Source:** docs/user-management.md (legacy static keys); plans/user-management.md.
- **Classification:** TO-BE (compatibility retention with documented migration path).
- **Priority:** Medium
- **Status:** Existing
- **Verification:** I
- **Evidence:** `ApiKeyAuthenticationHandler.cs` static configuration key validation CODE-OBSERVED; migration plan DOCUMENTED in docs/user-management.md.
- **Approval:** Review Required

### SR-A.005 — API keys carry the owner's role at generation

- **ID:** SR-A.005
- **Requirement:** The gateway shall give an API key the role of its generating user at generation time, so that a key generated by a DeviceAdmin continues to act as DeviceAdmin even if the owner's role later changes.
- **Rationale:** Makes the effective privilege of each key deterministic and auditable.
- **Source:** docs/user-management.md (Keys carry the role of their owner at the time of generation).
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** Medium
- **Status:** Existing
- **Verification:** D
- **Evidence:** `src/RTSec.Kryptonian.Api/Authentication/ApiKeyAuthenticationHandler.cs` (role from ApiKey record) CODE-OBSERVED; docs/user-management.md DOCUMENTED.
- **Approval:** Review Required

### SR-A.006 — JWT signing secret provisioning

- **ID:** SR-A.006
- **Requirement:** The gateway shall require an operator-supplied JWT signing secret of at least 32 characters in non-development environments and shall refuse to start (fail fast) when it is missing or matches a known development default.
- **Rationale:** The signing secret is the root of admin-session trust; silent fallback is a forgery vector.
- **Source:** Baseline finding (hard-coded dev fallback in `Program.cs`); KSPEC-002 UR-013; plans/user-management.md (≥32 chars; "dev fallback constant with warning").
- **Classification:** TO-BE (not satisfied at baseline).
- **Priority:** Critical
- **Status:** Missing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Program.cs` (fallback constant; no fail-fast check) CODE-OBSERVED; plans/user-management.md DOCUMENTED.
- **Approval:** Review Required

### SR-A.007 — EST bootstrap challenge behavior

- **ID:** SR-A.007
- **Requirement:** The gateway shall respond with HTTP 401 and a `WWW-Authenticate: Basic` challenge header when EST bootstrap credentials are absent or invalid, distinguishing missing credentials from rejected credentials.
- **Rationale:** Correct challenge semantics let standard clients and developers diagnose authentication failures.
- **Source:** KSPEC-002 UR-023; PR 42.
- **Classification:** TO-BE (normative statement of AS-IS behavior).
- **Priority:** Medium
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/EstController.cs` (BasicUnauthorized with WWW-Authenticate) CODE-OBSERVED; docs/formal-specification/05-INTERFACE-CONTROL.md (401 challenge row) DOCUMENTED.
- **Approval:** Review Required

### SR-A.008 — EST re-enrollment credential rule

- **ID:** SR-A.008
- **Requirement:** The gateway shall authenticate `simplereenroll` exclusively by the presented TLS client certificate, validated per SR-F-EST.009, and shall not accept any header- or body-supplied device identity as authentication evidence.
- **Rationale:** Header-supplied identity is spoofable; certificate possession is the only non-replayable renewal credential.
- **Source:** PR 38 (authenticated enrollment identity binding); EstController device-identifier rule (header logged but untrusted).
- **Classification:** TO-BE (normative statement of AS-IS behavior).
- **Priority:** Critical
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/EstController.cs` (GetDeviceIdentifier returns null for header; certificate subject used) CODE-OBSERVED.
- **Approval:** Review Required

### SR-A.009 — Development bypass confinement

- **ID:** SR-A.009
- **Requirement:** The gateway shall permit anonymous API-key bypass authentication only in the Development environment and only when no static API keys are configured, and shall never activate it in Production.
- **Rationale:** A development bypass must be structurally incapable of leaking into production.
- **Source:** ApiKeyAuthenticationHandler bypass gating; KSPEC-002 UR-015.
- **Classification:** TO-BE (normative statement of AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Authentication/ApiKeyAuthenticationHandler.cs` (bypass requires AllowAnonymousInDevelopment && no static keys && Development) CODE-OBSERVED.
- **Approval:** Review Required

### SR-A.010 — Login credential rejection without enumeration

- **ID:** SR-A.010
- **Requirement:** The gateway shall respond to failed portal login attempts with a generic authentication-failure response that does not reveal whether the username exists.
- **Rationale:** Account enumeration simplifies targeted attacks on administrative accounts.
- **Source:** docs/user-management.md (login); KSPEC-006 threat model input.
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** Medium
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/AuthController.cs` (uniform "Invalid username or password.") CODE-OBSERVED.
- **Approval:** Review Required

---

## 11. Authorization requirements (SR-Z)

### SR-Z.001 — Role model

- **ID:** SR-Z.001
- **Requirement:** The gateway shall enforce role-based access control with at least three roles — read-only (Standard), device administration (DeviceAdmin), and full system administration (SystemAdmin) — mapped to claims carried by both JWT and API-key identities.
- **Rationale:** Least-privilege separation of operational duties (view vs. device ops vs. system configuration).
- **Source:** KSPEC-002 UR-015 (Critical); docs/user-management.md (Roles).
- **Classification:** TO-BE (normative statement of AS-IS behavior).
- **Priority:** Critical
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Domain/Enums/UserRole.cs` (Standard, DeviceAdmin, SystemAdmin) CODE-OBSERVED; `src/RTSec.Kryptonian.Api/Program.cs` (StandardOrAbove, DeviceAdmin, SystemAdmin policies) CODE-OBSERVED.
- **Approval:** Review Required

### SR-Z.002 — Device administration restricted to DeviceAdmin and above

- **ID:** SR-Z.002
- **Requirement:** The gateway shall restrict device-creation, approval, trust modification, retirement, and demo-enrollment operations to callers holding DeviceAdmin or SystemAdmin roles.
- **Rationale:** Device operations change the trust state of the estate and must not be available to read-only callers.
- **Source:** KSPEC-002 UR-015 (UC-08).
- **Classification:** TO-BE (normative statement of AS-IS behavior).
- **Priority:** Critical
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/DevicesController.cs` (DeviceAdmin policies on mutating endpoints) CODE-OBSERVED; `src/RTSec.Kryptonian.Api/Controllers/CertificatesController.cs` (controller-level DeviceAdmin policy) CODE-OBSERVED.
- **Approval:** Review Required

### SR-Z.003 — System configuration restricted to SystemAdmin

- **ID:** SR-Z.003
- **Requirement:** The gateway shall restrict CA backend configuration, EST profile configuration, user administration, notification settings, and gateway settings modification to callers holding the SystemAdmin role.
- **Rationale:** Institution-wide policy and credential configuration demand the highest-privilege role.
- **Source:** KSPEC-002 UR-015; docs/user-management.md (SystemAdmin-only sections).
- **Classification:** TO-BE (normative statement of AS-IS behavior).
- **Priority:** Critical
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/CaBackendsController.cs`, `EstProfilesController.cs`, `UsersController.cs`, `NotificationSettingsController.cs`, `GatewaySettingsController.cs` (SystemAdmin policies on mutations) CODE-OBSERVED.
- **Approval:** Review Required

### SR-Z.004 — Read-only access for Standard role

- **ID:** SR-Z.004
- **Requirement:** The gateway shall permit Standard-role (and above) authenticated callers to access read-only inventory, status, and log endpoints, and shall deny them all mutating operations.
- **Rationale:** Audit and monitoring users need visibility without write power.
- **Source:** KSPEC-002 UR-015; `StandardOrAbove` policy.
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Program.cs` (StandardOrAbove policy) CODE-OBSERVED; controller-level `[Authorize]` on read endpoints CODE-OBSERVED.
- **Approval:** Review Required

### SR-Z.005 — Revocation restricted to device administrators

- **ID:** SR-Z.005
- **Requirement:** The gateway shall restrict certificate revocation and trust-removal operations to callers holding DeviceAdmin or SystemAdmin roles.
- **Rationale:** Revocation is a high-impact trust decision; it must not be performable by read-only callers.
- **Source:** KSPEC-002 UR-009, UR-010; docs/flow.md (Trust Removal Flow).
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** Critical
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/CertificatesController.cs` (DeviceAdmin policy) CODE-OBSERVED; `src/RTSec.Kryptonian.Api/Controllers/DevicesController.cs` (trust endpoints under DeviceAdmin) CODE-OBSERVED.
- **Approval:** Review Required

### SR-Z.006 — API-key requests inherit role claims

- **ID:** SR-Z.006
- **Requirement:** The gateway shall derive authorization claims for API-key-authenticated requests solely from the authenticated key record and its stored role, and shall not accept role claims supplied by the client.
- **Rationale:** Client-supplied role claims would enable trivial privilege escalation.
- **Source:** ApiKeyAuthenticationHandler (role from record); KSPEC-002 UR-027.
- **Classification:** TO-BE (normative statement of AS-IS behavior).
- **Priority:** Critical
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Authentication/ApiKeyAuthenticationHandler.cs` (BuildTicket role from apiKey record; no client claim ingestion) CODE-OBSERVED.
- **Approval:** Review Required

### SR-Z.007 — Explicit role policy on every mutating endpoint

- **ID:** SR-Z.007
- **Requirement:** The gateway shall enforce an explicit role policy (DeviceAdmin or SystemAdmin) on every state-changing administrative endpoint, including backend connectivity-test operations, rather than relying on authentication alone.
- **Rationale:** The backend test endpoint performs credential-bearing calls and is authentication-only at baseline; authentication without role policy is a weak control.
- **Source:** Baseline finding: `POST /api/cas/{id}/test` lacks a DeviceAdmin/SystemAdmin policy (KSPEC-005 open item 4).
- **Classification:** TO-BE (hardening of an AS-IS weak control).
- **Priority:** High
- **Status:** Partial
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/CaBackendsController.cs` (test endpoint `[Authorize]` without policy; SystemAdmin on configuration endpoints) CODE-OBSERVED; docs/formal-specification/05-INTERFACE-CONTROL.md open items DOCUMENTED.
- **Approval:** Review Required

### SR-Z.008 — Anonymous access limited to a documented set

- **ID:** SR-Z.008
- **Requirement:** The gateway shall restrict unauthenticated access to the documented set of endpoints — health check, self-service device approval request, EST protocol handlers, ACME challenge path, issuer CRL GET, authentication status/setup/login/logout handlers, and dashboard static assets — and shall require authentication on protected administrative endpoints; EST handlers shall enforce their own bootstrap or client-certificate authentication.
- **Rationale:** A complete anonymous-surface inventory is the precondition for perimeter reasoning.
- **Source:** Controller authorization attributes survey; KSPEC-005 open item 6.
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** I
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/` ([Authorize] coverage; explicit [AllowAnonymous] on device-requests; absence of [Authorize] on Auth/EST/CRL/challenge handlers) CODE-OBSERVED; `src/RTSec.Kryptonian.Api/Program.cs` (unauthenticated health map) CODE-OBSERVED.
- **Approval:** Review Required

### SR-Z.009 — Confirmed authorization model for self-service requests

- **ID:** SR-Z.009
- **Requirement:** The stakeholder-approved authorization model for the anonymous self-service device approval-request endpoint (who may call it, from where, and with what abuse controls) shall be recorded in this specification after review; the baseline intent (anonymous by design, pending-only creation) is `TBD` for confirmation.
- **Rationale:** The endpoint is a deliberate anonymous surface whose intended authorization model has not been stakeholder-confirmed.
- **Source:** KSPEC-005 open item 6; docs/formal-specification/05-INTERFACE-CONTROL.md.
- **Classification:** PROPOSED (stakeholder decision required).
- **Priority:** Medium
- **Status:** Proposed
- **Verification:** I
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/DevicesController.cs` (RequestApproval [AllowAnonymous]) CODE-OBSERVED; docs/formal-specification/05-INTERFACE-CONTROL.md (open item) DOCUMENTED.
- **Approval:** Review Required

### SR-Z.010 — No clinical capability derivation

- **ID:** SR-Z.010
- **Requirement:** No gateway state, role, certificate, or authorization decision shall be presented as, or shall be interpretable as, a device's clinical service status, clinical capability, SDC role authorization, or regulatory status.
- **Rationale:** Preserves the identity/capability boundary that is binding for the whole package (KSPEC-000 §3).
- **Source:** KSPEC-002 UR-026 (Critical); EXC-001/EXC-002.
- **Classification:** TO-BE (governing constraint).
- **Priority:** Critical
- **Status:** Existing
- **Verification:** I
- **Evidence:** docs/CHANGES-AND-ORNET-ALIGNMENT.md (renewal ≠ recertification); README.md disclaimer; docs/formal-specification/04-SOFTWARE-DESIGN.md (boundary) DOCUMENTED.
- **Approval:** Review Required

---

## 12. Certificate lifecycle policy requirements (SR-C)

### SR-C.001 — Profile policy bundle

- **ID:** SR-C.001
- **Requirement:** The gateway shall bind each EST profile to a named policy bundle containing a CA backend binding, subject-DN rules, key usage and EKU constraints, certificate validity, and trust policy, and shall enforce that bundle on every enrollment through the profile.
- **Rationale:** Certificate policy must be administratively controlled by the institution, not hard-coded or implied.
- **Source:** KSPEC-002 UR-031 (Critical); KSPEC-000 §9 (EST profile terminology).
- **Classification:** TO-BE (normative statement of AS-IS behavior).
- **Priority:** Critical
- **Status:** Existing
- **Verification:** I
- **Evidence:** `src/RTSec.Kryptonian.Domain/Entities/EstProfile.cs` and `src/RTSec.Kryptonian.Application/DTOs/EstProfileDto.cs` CODE-OBSERVED; docs/ca-backend-configuration.md DOCUMENTED.
- **Approval:** Review Required

### SR-C.002 — Trust policy durability across restarts and updates

- **ID:** SR-C.002
- **Requirement:** The gateway shall persist EST profile trust policy across restarts and shall apply trust-list changes additively, preserving omitted trust lists on partial updates.
- **Rationale:** Loss or silent narrowing of trust policy on restart or update would break enrolled devices unpredictably.
- **Source:** KSPEC-002 UR-032; PR 41, PR 47.
- **Classification:** TO-BE (normative statement of AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** T
- **Evidence:** docs/IMPLEMENTATION-TRACKER.md (restart persistence on populated SQLite VERIFIED; partial-update preservation PR 47); `src/RTSec.Kryptonian.Infrastructure/Data/SchemaUpgrades.cs` CODE-OBSERVED.
- **Approval:** Review Required

### SR-C.003 — Profile-configured validity periods

- **ID:** SR-C.003
- **Requirement:** The gateway shall derive certificate validity periods from the EST profile configuration, and the production certificate-lifetime policy (balancing outage tolerance against revocation objectives) is `TBD` pending institutional decision (KSPEC-001 TBD-001).
- **Rationale:** Lifetime is a security policy trade-off, not a code constant.
- **Source:** KSPEC-002 UR-031; TBD-001.
- **Classification:** TO-BE (elaborates AS-IS behavior; production values TBD).
- **Priority:** High
- **Status:** Existing
- **Verification:** I
- **Evidence:** `src/RTSec.Kryptonian.Application/DTOs/EstProfileDto.cs` (validity fields) CODE-OBSERVED; docs/ca-backend-configuration.md DOCUMENTED.
- **Approval:** Review Required

### SR-C.004 — Renewal window authorization

- **ID:** SR-C.004
- **Requirement:** The gateway shall authorize re-enrollment without re-approval only for devices that are active, unrevoked, and presenting a valid credential issued under the resolved profile.
- **Rationale:** The renewal window must not become a path for unapproved identity changes.
- **Source:** KSPEC-002 UR-021; KSPEC-000 §9 (Renewal window).
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** Critical
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Application/Services/EnrollmentOrchestrator.cs` (reenroll validation) CODE-OBSERVED; docs/device-enrollment.md (Renewal And Future Signing) DOCUMENTED.
- **Approval:** Review Required

### SR-C.005 — Issuer-scoped CRL publication

- **ID:** SR-C.005
- **Requirement:** The gateway shall publish gateway-generated CRLs scoped to the issuing CA and shall record CRL state (issue time, next update) per issuer so that peers retrieve the CRL matching the issuer of each certificate.
- **Rationale:** An issuer-unscoped CRL could misstate revocation status for credentials from other issuers.
- **Source:** PR 46 (issuer-scoped signed CRLs); KSPEC-002 UR-010.
- **Classification:** TO-BE (normative statement of AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Domain/Entities/IssuerCrlState.cs` and `src/RTSec.Kryptonian.Infrastructure/Repositories/IssuerCrlStateRepository.cs` CODE-OBSERVED; `src/RTSec.Kryptonian.Api/Controllers/CrlController.cs` CODE-OBSERVED.
- **Approval:** Review Required

### SR-C.006 — CRL distribution points on issued certificates

- **ID:** SR-C.006
- **Requirement:** The gateway shall include a CRL distribution point extension in issued device certificates pointing at the gateway's CRL publication endpoint for the issuing CA.
- **Rationale:** Peers can only check revocation online if issued certificates carry a reachable distribution point.
- **Source:** PR 46 (CRL distribution points); KSPEC-002 UR-010.
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Infrastructure/Crypto/SelfSignedCaConnector.cs` and CRL issuance path CODE-OBSERVED; docs/CHANGES-AND-ORNET-ALIGNMENT.md PR 46 row DOCUMENTED.
- **Approval:** Review Required

### SR-C.007 — Online revocation checking at peers

- **ID:** SR-C.007
- **Requirement:** Reference peers and the demonstration components shall validate revocation status online through the gateway-published CRL path, rejecting revoked certificates independently of any local registry lookup.
- **Rationale:** A revoked credential is worthless if peers do not check the published revocation data.
- **Source:** PR 45 (explicit EST client trust and online revocation); docs/IMPLEMENTATION-TRACKER.md.
- **Classification:** AS-IS (reference-client behavior; demonstration-scoped).
- **Priority:** High
- **Status:** Existing
- **Verification:** D
- **Evidence:** docs/IMPLEMENTATION-TRACKER.md (administrator revocation followed by native online X509Chain reporting Revoked, independent of registry lookup) VERIFIED; `src/Kryptonian.DICOMTls/` CODE-OBSERVED.
- **Approval:** Review Required

### SR-C.008 — Revocation propagation semantics

- **ID:** SR-C.008
- **Requirement:** The system shall state explicitly that revocation propagation to established sessions is bounded (peers refresh CRL data on a timer and associations have bounded lifetimes), and no requirement shall promise instant mid-association termination; the stale-revocation-data policy is `TBD` pending stakeholder agreement.
- **Rationale:** Honest propagation semantics prevent operators from assuming instantaneous enforcement that does not exist.
- **Source:** EXC-007; KSPEC-004 AS-IS finding A-5; docs/IMPLEMENTATION-TRACKER.md (bounded proxy lifetimes).
- **Classification:** TO-BE (normative statement; AS-IS bounded behavior to be made explicit).
- **Priority:** Medium
- **Status:** Partial
- **Verification:** A
- **Evidence:** docs/development.md (associations have a bounded lifetime) DOCUMENTED; docs/IMPLEMENTATION-TRACKER.md DOCUMENTED; stale-revocation policy undefined UNKNOWN.
- **Approval:** Review Required

### SR-C.009 — Separation of revocation and enrollment denial

- **ID:** SR-C.009
- **Requirement:** The gateway shall treat administrative revocation of a device's certificate and removal of the device's trust as separable operations, each individually recorded, and shall not conflate their outcomes.
- **Rationale:** "Deny further enrollment" and "peers reject an issued certificate" are distinct security effects with distinct audit needs.
- **Source:** PR 46 (separates deny-further-enrollment from peer rejection); KSPEC-002 UR-009/UR-010.
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** I
- **Evidence:** `src/RTSec.Kryptonian.Application/Services/CertificateRevocationService.cs` and `EnrollmentOrchestrator.cs` CODE-OBSERVED; docs/CHANGES-AND-ORNET-ALIGNMENT.md PR 46 row DOCUMENTED.
- **Approval:** Review Required

### SR-C.010 — Enrollment event record per issuance attempt

- **ID:** SR-C.010
- **Requirement:** The gateway shall record an enrollment event for every enrollment, renewal, and revocation attempt, capturing the outcome, the issued certificate serial or error message, and the requester source address.
- **Rationale:** Lifecycle forensics and renewal monitoring both depend on complete per-attempt records.
- **Source:** KSPEC-002 UR-011; KSPEC-000 §9 (Enrollment event).
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** High
- **Status:** Partial
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Application/Services/EnrollmentEventService.cs` CODE-OBSERVED; `src/RTSec.Kryptonian.Application/DTOs/EnrollmentEventDto.cs` CODE-OBSERVED; docs/flow.md DOCUMENTED.
- **AS-IS limitation:** Some orchestrator failures return before event creation; controller authentication/media/size failures and revocation service paths do not create EnrollmentEvent records. Complete per-attempt lifecycle audit coverage is not implemented. VT-059 specifies the missing acceptance test.
- **Approval:** Review Required

---

## 13. Data management requirements (SR-D)

### SR-D.001 — Durable persistence of all configured state

- **ID:** SR-D.001
- **Requirement:** The gateway shall persist all configured state — devices, certificates, EST profiles, CA backends, trust policy, users, API keys, and notification settings — durably so that a gateway restart preserves the configuration and records.
- **Rationale:** Configuration loss across restarts would silently revoke operational trust state.
- **Source:** KSPEC-002 UR-037; PR 41.
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** T
- **Evidence:** docs/IMPLEMENTATION-TRACKER.md (gateway restart on populated SQLite preserved configured profile, issuer and devices) VERIFIED; `src/RTSec.Kryptonian.Infrastructure/Data/KryptonianDbContext.cs` CODE-OBSERVED.
- **Approval:** Review Required

### SR-D.002 — SQLite and PostgreSQL persistence support

- **ID:** SR-D.002
- **Requirement:** The gateway shall support both an embeddable persistence option (SQLite) and a server-grade persistence option (PostgreSQL) selectable by configuration.
- **Rationale:** Hospitals need an embeddable option for small sites and a server-grade option for production estates.
- **Source:** KSPEC-002 UR-037; docs/DEPLOYMENT.md; TBD-006.
- **Classification:** TO-BE (elaborates AS-IS behavior; PostgreSQL exercised: UNKNOWN).
- **Priority:** High
- **Status:** Partial
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Infrastructure/Data/` (Npgsql and Sqlite providers) CODE-OBSERVED; SQLite persistence VERIFIED per docs/IMPLEMENTATION-TRACKER.md; PostgreSQL unexercised UNKNOWN.
- **Approval:** Review Required

### SR-D.003 — Additive schema upgrades that never recreate data

- **ID:** SR-D.003
- **Requirement:** The gateway shall apply schema upgrades additively and idempotently (adding columns, tables, and indexes; never dropping or recreating), and shall never recreate a populated database.
- **Rationale:** Recreating a populated database would destroy live trust state.
- **Source:** `SchemaUpgrades.cs` header rule ("keep this additive: never recreate a populated database"); PR 41.
- **Classification:** TO-BE (normative statement of AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** I
- **Evidence:** `src/RTSec.Kryptonian.Infrastructure/Data/SchemaUpgrades.cs` (additive DDL shapes, header comment) CODE-OBSERVED; docs/formal-specification/07-OPERATIONAL-RISK.md DOCUMENTED.
- **Approval:** Review Required

### SR-D.004 — Upgrade safety boundary

- **ID:** SR-D.004
- **Requirement:** The gateway shall execute schema upgrades within an explicit safety boundary — a pre-upgrade backup and an explicit upgrade transaction — before applying additive DDL to an existing database.
- **Rationale:** Baseline upgrades run as raw DDL outside an explicit transaction/backup boundary; a mid-upgrade failure could leave an inconsistent schema.
- **Source:** KSPEC-010 gap input (upgrade DDL outside explicit transaction/backup boundary, per KSPEC-007 §backup).
- **Classification:** TO-BE (hardening of an AS-IS weak control).
- **Priority:** High
- **Status:** Missing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Infrastructure/Data/SchemaUpgrades.cs` (raw additive DDL) CODE-OBSERVED; docs/formal-specification/07-OPERATIONAL-RISK.md (restore sequence places SchemaUpgrades after restore) DOCUMENTED.
- **Approval:** Review Required

### SR-D.005 — UTC timestamp discipline

- **ID:** SR-D.005
- **Requirement:** The gateway shall store and compute all certificate and revocation timestamps as UTC end-to-end, including round-trips through the database, so that no reload path shifts times across time zones.
- **Rationale:** An unspecified DateTime kind shifted refreshed CRL revocation times five hours into the future on one host; such shifts silently corrupt revocation semantics.
- **Source:** docs/IMPLEMENTATION-TRACKER.md (SQLite DateTime-kind bug fixed with a runnable regression).
- **Classification:** TO-BE (normative statement of AS-IS behavior after fix).
- **Priority:** High
- **Status:** Existing
- **Verification:** T
- **Evidence:** docs/IMPLEMENTATION-TRACKER.md (fix with runnable regression) VERIFIED; `src/RTSec.Kryptonian.Infrastructure/Data/` DateTime handling CODE-OBSERVED.
- **Approval:** Review Required

### SR-D.006 — Lifecycle record retention

- **ID:** SR-D.006
- **Requirement:** The gateway shall retain enrollment events and certificate records for a configurable retention period sufficient for audit reconstruction; the production retention period is `TBD` pending institutional retention policy.
- **Rationale:** Retention governs how far incident reconstruction can reach; unbounded or accidental deletion both fail audits.
- **Source:** KSPEC-002 UR-011; KSPEC-007 (audit scenarios).
- **Classification:** TO-BE (retention policy not implemented at baseline).
- **Priority:** Medium
- **Status:** Partial
- **Verification:** I
- **Evidence:** `EnrollmentEventRepository.cs` retention policy not enforced CODE-OBSERVED; production retention period TBD.
- **Approval:** Review Required

### SR-D.007 — Protected-data key persistence

- **ID:** SR-D.007
- **Requirement:** The gateway shall persist the Data Protection key ring to a durable, deployment-configured location in every production deployment mode, and shall document key-ring backup/restore as part of the recovery procedure.
- **Rationale:** Protected fields become unreadable if the key ring is lost; sandbox persistence exists but general production persistence is not evidenced.
- **Source:** `Program.cs` sandbox key-ring persistence; docs/formal-specification/07-OPERATIONAL-RISK.md (restore Data Protection key ring first).
- **Classification:** TO-BE (elaborates AS-IS behavior; production persistence policy UNKNOWN).
- **Priority:** High
- **Status:** Partial
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Program.cs` (PersistKeysToFileSystem under sandbox only) CODE-OBSERVED; docs/formal-specification/07-OPERATIONAL-RISK.md (key ring first in restore order) DOCUMENTED.
- **Approval:** Review Required

### SR-D.008 — Sensitive field encryption at rest

- **ID:** SR-D.008
- **Requirement:** The gateway shall apply field-level encryption (via the application's data-protection mechanism) to stored SMTP passwords, connector credentials, and CA key passwords rather than storing them as plaintext database columns.
- **Rationale:** Database dumps must not expose institutional credentials.
- **Source:** KSPEC-002 UR-013 (Missing); DataProtectionService interface.
- **Classification:** TO-BE (not implemented at baseline).
- **Priority:** High
- **Status:** Missing
- **Verification:** I
- **Evidence:** `src/RTSec.Kryptonian.Domain/Entities/NotificationSettings.cs` and CA backend entity field types CODE-OBSERVED; `src/RTSec.Kryptonian.Infrastructure/Security/DataProtectionService.cs` CODE-OBSERVED; field coverage not evidenced UNKNOWN.
- **Approval:** Review Required

### SR-D.009 — Demonstration operations distinguishable

- **ID:** SR-D.009
- **Requirement:** The gateway shall make demonstration operations (demo enrollment, demo device creation) distinguishable from production lifecycle records in stored events and portal views.
- **Rationale:** Mixed demo and production records contaminate inventory and audit interpretation.
- **Source:** docs/IMPLEMENTATION-TRACKER.md (synthetic demo records remain); demo-enroll endpoint.
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** Low
- **Status:** Partial
- **Verification:** I
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/DevicesController.cs` (DemoEnroll as a distinct operation) CODE-OBSERVED; explicit demo flagging in stored records not evidenced UNKNOWN.
- **Approval:** Review Required

---

## 14. Performance requirements (SR-P)

### SR-P.001 — EST rate-limit capacity

- **ID:** SR-P.001
- **Requirement:** The gateway shall accept sustained EST request rates up to the configured per-IP limit (baseline rules: 100 requests/minute on `*:/.well-known/est/*`, with more restrictive 30 requests/minute rules for labeled enrollment and re-enrollment POST paths) without error and shall throttle beyond it with HTTP 429; the production capacity target is `TBD` pending load testing.
- **Rationale:** Enrollment bursts (e.g., estate-wide renewal) must be absorbed without failure while abuse is throttled.
- **Source:** docs/ARCHITECTURE.md (rate limiting configuration); KSPEC-002 UR-014.
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** Medium
- **Status:** Partial
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/appsettings.json` (IpRateLimiting) CODE-OBSERVED; load-test results not present UNKNOWN.
- **AS-IS limitation:** Configured limits do not establish throughput, latency or availability below the limit. Middleware rule matching for unlabeled versus labeled paths and client-IP attribution needs VT-055 execution. `X-Forwarded-For` supplies the rate-limit key without an authenticated-forwarder proof in this baseline.
- **Approval:** Review Required

### SR-P.002 — Enrollment request size bounds

- **ID:** SR-P.002
- **Requirement:** The gateway shall limit EST CSR request bodies to 64 KiB and shall enforce the limit before cryptographic processing.
- **Rationale:** Bounding input size protects memory and limits denial-of-service amplification.
- **Source:** EstController MaxCsrBodySize; KSPEC-005 ICD-EST.
- **Classification:** TO-BE (normative statement of AS-IS behavior).
- **Priority:** Medium
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/EstController.cs` (RequestSizeLimit, 413 handling) CODE-OBSERVED.
- **Approval:** Review Required

### SR-P.003 — Enrollment latency targets

- **ID:** SR-P.003
- **Requirement:** The gateway shall complete a local-CA enrollment interaction within a latency budget agreed with stakeholders; the target values are `TBD` pending measurement (no performance budget exists at baseline).
- **Rationale:** Device-side renewal timeouts depend on gateway latency; an unagreed budget cannot be verified.
- **Source:** KSPEC-002 UR-021 (automation implies bounded latency); no baseline performance budget found.
- **Classification:** PROPOSED (targets require stakeholder agreement and measurement).
- **Priority:** Low
- **Status:** Missing
- **Verification:** A
- **Evidence:** No performance-threshold test or measurement found in tests/ or docs (searched) UNKNOWN; docs/ARCHITECTURE.md performance section (rate limits only) DOCUMENTED.
- **Approval:** Review Required

### SR-P.004 — Database connection pooling

- **ID:** SR-P.004
- **Requirement:** The gateway shall use pooled database connections with a configurable pool size for PostgreSQL deployments.
- **Rationale:** Estate-scale polling (dashboards, watchers) exhausts small connection pools.
- **Source:** docs/ARCHITECTURE.md (Npgsql pooling); DevicesController AllCertificates N+1 note.
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** Low
- **Status:** Existing
- **Verification:** I
- **Evidence:** `src/RTSec.Kryptonian.Api/Program.cs` (Npgsql configuration) CODE-OBSERVED; docs/ARCHITECTURE.md (MaxPoolSize 100 example) DOCUMENTED; `src/RTSec.Kryptonian.Api/Controllers/DevicesController.cs` (AllCertificates N+1 avoidance comment) CODE-OBSERVED.
- **Approval:** Review Required

### SR-P.005 — Profile and CA certificate caching

- **ID:** SR-P.005
- **Requirement:** The gateway shall cache EST profile lookups and CA certificate chains in memory so repeated device requests do not re-fetch unchanged CA material, with cache invalidation on profile or backend configuration change.
- **Rationale:** Enrollment is a control-plane operation; avoidable repeated I/O on the hot path shortens latency under bursts.
- **Source:** docs/ARCHITECTURE.md (Certificate Caching).
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** Low
- **Status:** Partial
- **Verification:** I
- **Evidence:** docs/ARCHITECTURE.md (CA certificates cached in memory; profile lookups cached by path/hostname) DOCUMENTED; invalidation behavior not separately evidenced UNKNOWN.
- **Approval:** Review Required

---

## 15. Reliability requirements (SR-R)

### SR-R.001 — Background watcher fault tolerance

- **ID:** SR-R.001
- **Requirement:** The gateway's background services shall catch and log scan failures and shall continue operating on the next cycle rather than terminating the gateway process.
- **Rationale:** A transient database or notification failure must not crash the enrollment service.
- **Source:** CertificateExpiryWatcher design; KSPEC-002 UR-012.
- **Classification:** TO-BE (normative statement of AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/HostedServices/CertificateExpiryWatcher.cs` (try/catch around ScanOnceAsync, logging, loop continues) CODE-OBSERVED.
- **Approval:** Review Required

### SR-R.002 — Backend failure outcomes recorded

- **ID:** SR-R.002
- **Requirement:** The gateway shall surface transient CA backend failures as distinguishable error outcomes to the device and as recorded enrollment events, without partial or intermediate trust state persisting.
- **Rationale:** Operators must be able to distinguish "backend down" from "request rejected," and no orphaned state may result.
- **Source:** KSPEC-002 UR-023; PR 46 (transactional design).
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Application/Services/EnrollmentOrchestrator.cs` (error result paths, event recording) CODE-OBSERVED; docs/API-GUIDE.md (500 mapping) DOCUMENTED.
- **Approval:** Review Required

### SR-R.003 — Notification delivery decoupled from lifecycle state

- **ID:** SR-R.003
- **Requirement:** The gateway shall ensure that a failed notification delivery does not prevent, roll back, or corrupt the lifecycle state change that triggered it; delivery-reliability semantics (retries, dead-lettering) are `TBD` (TBD-002).
- **Rationale:** Email transport is best-effort; trust state changes must be atomic and independent.
- **Source:** KSPEC-002 UR-012; `NotificationDispatcher` design.
- **Classification:** TO-BE (elaborates AS-IS behavior; delivery reliability UNKNOWN).
- **Priority:** Medium
- **Status:** Partial
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Application/Notifications/NotificationDispatcher.cs` CODE-OBSERVED; `CertificateExpiryWatcher` records LastExpiryNotifiedAt only after dispatch CODE-OBSERVED; delivery behavior not exercised at baseline DOCUMENTED.
- **Approval:** Review Required

### SR-R.004 — Concurrency conflict handling

- **ID:** SR-R.004
- **Requirement:** The gateway shall detect concurrent conflicting updates to lifecycle state (revocation, device status) and shall reject the losing operation with HTTP 409 instead of overwriting.
- **Rationale:** Silent last-writer-wins on revocation could un-revoke a certificate.
- **Source:** PR 46 (concurrent-update protection).
- **Classification:** TO-BE (normative statement of AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Application/Services/CertificateRevocationService.cs` and `src/RTSec.Kryptonian.Infrastructure/Data/UnitOfWork.cs` CODE-OBSERVED; docs/formal-specification/07-OPERATIONAL-RISK.md DOCUMENTED.
- **Approval:** Review Required

### SR-R.005 — Graceful shutdown

- **ID:** SR-R.005
- **Requirement:** The gateway shall honor cancellation during shutdown so that background scans and in-flight enrollment operations stop cleanly without corrupting persisted state.
- **Rationale:** Service stop/restart (Windows service or container) is routine and must be state-safe.
- **Source:** Hosted service cancellation handling; KSPEC-002 UR-037.
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** Medium
- **Status:** Existing
- **Verification:** I
- **Evidence:** `src/RTSec.Kryptonian.Api/HostedServices/CertificateExpiryWatcher.cs` (stoppingToken handling, OperationCanceledException paths) CODE-OBSERVED; `src/RTSec.Kryptonian.Infrastructure/Acme/Http01ChallengeCleanupService.cs` CODE-OBSERVED.
- **Approval:** Review Required

---

## 16. Availability requirements (SR-V)

### SR-V.001 — Health-check endpoint availability

- **ID:** SR-V.001
- **Requirement:** The gateway shall expose an unauthenticated health-check endpoint that remains available even when authentication, EST profile, or CA backend subsystems are degraded.
- **Rationale:** A health probe that dies with its dependencies cannot tell an orchestrator anything useful.
- **Source:** KSPEC-002 UR-029; docs/DEPLOYMENT.md (liveness probes).
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Program.cs` (MapHealthChecks before auth-dependent routing; AddDbContextCheck) CODE-OBSERVED; `src/RTSec.Kryptonian.Api/Controllers/StatusController.cs` CODE-OBSERVED.
- **Approval:** Review Required

### SR-V.002 — Component-level health reporting

- **ID:** SR-V.002
- **Requirement:** The gateway's health reporting shall include the health of its CA backend connectors in addition to the database, reporting each component's state so orchestration can distinguish storage loss from CA outage.
- **Rationale:** CA connector outages and database outages need different operator responses.
- **Source:** docs/ARCHITECTURE.md (health checks example listing database and ca_connectors); KSPEC-002 UR-029.
- **Classification:** TO-BE (partially satisfied: database check implemented; connector checks not evidenced).
- **Priority:** Medium
- **Status:** Missing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Program.cs` (only AddDbContextCheck registered) CODE-OBSERVED; docs/ARCHITECTURE.md (ca_connectors component documented) DOCUMENTED.
- **Approval:** Review Required

### SR-V.003 — Restart state preservation

- **ID:** SR-V.003
- **Requirement:** A gateway restart on a populated database shall preserve configured profiles, issuers, devices, and certificates, and shall resume normal operation without operator intervention.
- **Rationale:** Trust state must not evaporate across routine restarts.
- **Source:** KSPEC-002 UR-037; docs/IMPLEMENTATION-TRACKER.md.
- **Classification:** TO-BE (normative statement of AS-IS behavior).
- **Priority:** Critical
- **Status:** Existing
- **Verification:** T
- **Evidence:** docs/IMPLEMENTATION-TRACKER.md (gateway restart on populated SQLite preserved configured profile, issuer and devices) VERIFIED.
- **Approval:** Review Required

### SR-V.004 — Availability targets

- **ID:** SR-V.004
- **Requirement:** The deployed gateway shall meet institutionally agreed availability targets for the enrollment service; the target values and measurement window are `TBD` pending stakeholder agreement.
- **Rationale:** An enrollment gateway is on the device provisioning critical path; targets must be explicit to be operable.
- **Source:** KSPEC-007 operational risk (gateway unavailability scenario); no baseline target exists.
- **Classification:** PROPOSED (stakeholder decision required).
- **Priority:** Medium
- **Status:** Missing
- **Verification:** A
- **Evidence:** docs/formal-specification/07-OPERATIONAL-RISK.md (availability scenario) DOCUMENTED; no SLA/target in repository UNKNOWN.
- **Approval:** Review Required

---

## 17. Audit requirements (SR-U)

### SR-U.001 — Audit record for every trust decision

- **ID:** SR-U.001
- **Requirement:** The gateway shall record an audit record for every enrollment, renewal, and revocation decision, including requester identity, source address, CSR subject, outcome, and reason.
- **Rationale:** Security teams must be able to reconstruct who enrolled what, from where, through which backend.
- **Source:** KSPEC-002 UR-011 (High); Mvp.md §8 ("audit everything").
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** High
- **Status:** Partial
- **Verification:** I
- **Evidence:** `src/RTSec.Kryptonian.Application/Services/EnrollmentEventService.cs` and `src/RTSec.Kryptonian.Application/DTOs/EnrollmentEventDto.cs` CODE-OBSERVED; docs/flow.md (Enrollment Events) DOCUMENTED.
- **AS-IS limitation:** Selected issuance/rejection paths persist events, but early CSR/profile/credential failures and revocation outcomes are not uniformly persisted as EnrollmentEvent records. Request logging and certificate-state mutation are not substitutes for a complete durable decision audit. See VT-059 and GAP-038.
- **Approval:** Review Required

### SR-U.002 — Enrollment event completeness

- **ID:** SR-U.002
- **Requirement:** The gateway shall record blocked and failed enrollment attempts (including trust-removal blocks and policy rejections) as enrollment events with a reason, not only successful issuances.
- **Rationale:** Blocked attempts are the primary evidence of misuse probing.
- **Source:** KSPEC-002 UR-009 (blocked enrollment event with reason); docs/flow.md.
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** High
- **Status:** Partial
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Application/Services/EnrollmentOrchestrator.cs` (failure event paths, ErrorMessage capture) CODE-OBSERVED; `DevicesController.Events` mapping (ErrorMessage / serial detail) CODE-OBSERVED.
- **AS-IS limitation:** Selected issuance/rejection paths persist events, but early CSR/profile/credential failures and revocation outcomes are not uniformly persisted as EnrollmentEvent records. Request logging and certificate-state mutation are not substitutes for a complete durable decision audit. See VT-059 and GAP-038.
- **Approval:** Review Required


### SR-U.003 — Structured, correlated logging

- **ID:** SR-U.003
- **Requirement:** The gateway shall emit structured logs with a correlation ID, request ID, and client IP for every HTTP request, and shall write daily rolling log files.
- **Rationale:** Correlated, structured logs are the transport-level complement to enrollment events for incident analysis.
- **Source:** Serilog configuration; `RequestIdMiddleware`; docs/ARCHITECTURE.md (Observability).
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** Medium
- **Status:** Existing
- **Verification:** I
- **Evidence:** `src/RTSec.Kryptonian.Api/Program.cs` (Serilog with CorrelationId/RequestId/ClientIp enrichment, rolling file) CODE-OBSERVED; `src/RTSec.Kryptonian.Api/Middleware/RequestIdMiddleware.cs` CODE-OBSERVED.
- **Approval:** Review Required

### SR-U.004 — Administrative action logging

- **ID:** SR-U.004
- **Requirement:** The gateway shall log every administrative state-changing action (user, key, device, backend, profile, and settings changes) with the acting identity and the affected resource.
- **Rationale:** Configuration changes can shift trust for the entire estate; attribution is mandatory.
- **Source:** KSPEC-002 UR-011; docs/user-management.md (audit review).
- **Classification:** TO-BE (elaborates AS-IS behavior; coverage beyond enrollment events not evidenced).
- **Priority:** High
- **Status:** Partial
- **Verification:** I
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/UsersController.cs` and other controllers (logging warnings on failures) CODE-OBSERVED; systematic admin-action audit records not evidenced UNKNOWN.
- **Approval:** Review Required

### SR-U.005 — Audit record tamper protection

- **ID:** SR-U.005
- **Requirement:** The gateway shall protect audit records against undetected modification or deletion by any gateway operator account (e.g., append-only storage or external forwarding); the selected mechanism is `TBD` pending design approval.
- **Rationale:** An attacker with admin access can otherwise erase the evidence of their own actions.
- **Source:** KSPEC-006 threat model input; KSPEC-002 UR-011 rationale (reconstruction implies integrity).
- **Classification:** PROPOSED (mechanism requires design approval; not implemented at baseline).
- **Priority:** Medium
- **Status:** Missing
- **Verification:** A
- **Evidence:** `src/RTSec.Kryptonian.Infrastructure/Repositories/EnrollmentEventRepository.cs` (no append-only mechanism) CODE-OBSERVED; no tamper-protection evidence in docs UNKNOWN.
- **Approval:** Review Required

### SR-U.006 — Audit export capability

- **ID:** SR-U.006
- **Requirement:** The gateway shall provide a supported export path for lifecycle audit events suitable for institutional SIEM consumption; mapping to hospital audit transport standards is a separate conformance task and no standard-conformant export is claimed at baseline.
- **Rationale:** Audit value depends on reaching the institution's central monitoring, not only the local portal.
- **Source:** KSPEC-004 finding A-6 (audit is a database table + structured logs; ATNA mapping TO-BE/PROPOSED).
- **Classification:** PROPOSED (scope requires approval).
- **Priority:** Low
- **Status:** Missing
- **Verification:** I
- **Evidence:** docs/formal-specification/04-SOFTWARE-DESIGN.md A-6 DOCUMENTED; no export endpoint found in controllers CODE-OBSERVED.
- **Approval:** Review Required

### SR-U.007 — Live log ring buffer bounds

- **ID:** SR-U.007
- **Requirement:** The gateway's live activity feed shall be bounded (ring buffer) so that log volume cannot grow memory unboundedly, and the persisted daily log files shall remain the authoritative record.
- **Rationale:** A live feed is a convenience; unbounded buffering would be a denial-of-service on the gateway itself.
- **Source:** LiveLogBroadcaster design; docs/ARCHITECTURE.md (Observability).
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** Low
- **Status:** Existing
- **Verification:** I
- **Evidence:** `src/RTSec.Kryptonian.Api/Program.cs` (LiveLogSink/LiveLogBroadcaster singleton) CODE-OBSERVED; docs/ARCHITECTURE.md DOCUMENTED.
- **Approval:** Review Required

---

## 18. Deployment requirements (SR-B)

### SR-B.001 — Deployment packaging options

- **ID:** SR-B.001
- **Requirement:** The gateway shall be deployable as containers (Docker Compose and Kubernetes manifests) and as a Windows package, with deployment guidance for each option.
- **Rationale:** Hospitals have heterogeneous infrastructure; deployment flexibility determines adoptability.
- **Source:** KSPEC-002 UR-028 (Medium); docs/DEPLOYMENT.md; docker-compose.yml; deploy/; installer/windows/.
- **Classification:** TO-BE (elaborates AS-IS behavior; Windows installer path and PostgreSQL in practice UNKNOWN, TBD-006).
- **Priority:** Medium
- **Status:** Existing
- **Verification:** I
- **Evidence:** `docker-compose.yml`, `deploy/azure/`, `installer/windows/` CODE-OBSERVED; docs/DEPLOYMENT.md and docs/windows-installation.md DOCUMENTED.
- **Approval:** Review Required

### SR-B.002 — Windows service and sandbox modes

- **ID:** SR-B.002
- **Requirement:** The gateway shall support hosting as a Windows service, and a sandbox mode that generates local demonstration services and is refused when the environment is not Production.
- **Rationale:** Sandbox demonstration services must never silently activate outside their intended environment.
- **Source:** SandboxBootstrap (Production requirement); docs/windows-sandbox.md.
- **Classification:** TO-BE (normative statement of AS-IS behavior).
- **Priority:** Medium
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Program.cs` (sandbox initialization, "sandbox services must run in Production" exception, UseWindowsService) CODE-OBSERVED; `src/RTSec.Kryptonian.Api/SandboxBootstrap.cs` CODE-OBSERVED.
- **Approval:** Review Required

### SR-B.003 — External TLS certificate provisioning

- **ID:** SR-B.003
- **Requirement:** The gateway shall accept operator-provided TLS certificates for its HTTPS endpoints in production deployments and shall reserve self-signed certificate generation for development and sandbox modes.
- **Rationale:** Production gateways must present institutionally controlled server identities.
- **Source:** `SelfSignedCaGenerator.cs`; docs/ca-backend-configuration.md; SandboxBootstrap.
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** I
- **Evidence:** `src/RTSec.Kryptonian.Api/SelfSignedCaGenerator.cs` CODE-OBSERVED; `src/RTSec.Kryptonian.Api/Program.cs` (Kestrel HTTPS configuration, sandbox PFX loading) CODE-OBSERVED; docs/DEPLOYMENT.md DOCUMENTED.
- **Approval:** Review Required

### SR-B.004 — Configuration via documented settings

- **ID:** SR-B.004
- **Requirement:** The gateway shall source all environment-specific configuration (database, TLS, authentication, rate limits, backends) from documented configuration keys and environment variables, and shall document each key's default and effect.
- **Rationale:** Undocumented configuration drift makes deployment behavior irreproducible.
- **Source:** KSPEC-005 ICD-CFG (configuration item list); docs/DEPLOYMENT.md.
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** Medium
- **Status:** Existing
- **Verification:** I
- **Evidence:** `src/RTSec.Kryptonian.Api/appsettings.json` CODE-OBSERVED; docs/formal-specification/05-INTERFACE-CONTROL.md (configuration tables) DOCUMENTED.
- **Approval:** Review Required

### SR-B.005 — CRL-over-HTTP listener restriction

- **ID:** SR-B.005
- **Requirement:** When the CRL-over-HTTP option is enabled, the gateway shall serve unencrypted HTTP requests only for GET/HEAD of `/api/crl` paths and shall refuse all other unencrypted requests with HTTP 403.
- **Rationale:** Some CRL consumers cannot use HTTPS; the exception must be as narrow as possible.
- **Source:** Program.cs CrlOnlyHttp middleware; KSPEC-005 ICD-CFG.
- **Classification:** TO-BE (normative statement of AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** T
- **Evidence:** `Program.cs` CrlOnlyHttp and sandbox listener gate; `SandboxBootstrap.AllowsRequest` CODE-OBSERVED; `SandboxBootstrapTests.cs` test definitions CODE-OBSERVED.
- **Approval:** Review Required

### SR-B.006 — Upgrade preservation

- **ID:** SR-B.006
- **Requirement:** The Windows installer upgrade path shall preserve the existing CA material and database, shall run a readiness health check, and shall not delete existing state to work around failures.
- **Rationale:** Upgrades that discard trust state or paper over failures create hidden breakage.
- **Source:** docs/formal-specification/07-OPERATIONAL-RISK.md (MSI upgrade preserves existing CA and database; readiness health check); artifacts/windows-installer/payload/docs/windows-installation.md (do not delete existing CA/database files).
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** Medium
- **Status:** Existing
- **Verification:** D
- **Evidence:** `installer/windows/` CODE-OBSERVED; artifacts/windows-installer/payload/docs/windows-installation.md DOCUMENTED; docs/formal-specification/07-OPERATIONAL-RISK.md DOCUMENTED.
- **Approval:** Review Required

### SR-B.007 — Production secret provisioning

- **ID:** SR-B.007
- **Requirement:** Production deployment guidance shall require all secrets (JWT secret, static API keys, connector credentials, SMTP credentials) to be supplied through the deployment's secret mechanism, and shall treat plaintext configuration-file secrets as development-only.
- **Rationale:** Deployment guidance is the last control layer against secret sprawl.
- **Source:** KSPEC-002 UR-013; KSPEC-004 PROPOSED item P-1; docs/DEPLOYMENT.md.
- **Classification:** TO-BE (guidance strengthened; baseline uses env/plain config).
- **Priority:** High
- **Status:** Partial
- **Verification:** I
- **Evidence:** docs/DEPLOYMENT.md DOCUMENTED; `src/RTSec.Kryptonian.Api/appsettings.Development.json` CODE-OBSERVED; docs/formal-specification/04-SOFTWARE-DESIGN.md P-1 DOCUMENTED.
- **Approval:** Review Required

---

## 19. Operational maintenance requirements (SR-O)

### SR-O.001 — Extensible connector architecture

- **ID:** SR-O.001
- **Requirement:** The gateway's design shall allow a new CA backend type to be added by implementing the internal connector abstraction without changing device-facing behavior or enrollment business logic.
- **Rationale:** Sustains the multi-CA roadmap and the single-protocol device contract as backends evolve.
- **Source:** KSPEC-002 UR-038 (Medium); docs/ARCHITECTURE.md (Extension Points).
- **Classification:** TO-BE (elaborates AS-IS design).
- **Priority:** Medium
- **Status:** Existing
- **Verification:** A
- **Evidence:** `src/RTSec.Kryptonian.Infrastructure/Crypto/CaConnectorFactory.cs` and `src/RTSec.Kryptonian.Domain/Interfaces/` (ICaConnector) CODE-OBSERVED; docs/ARCHITECTURE.md DOCUMENTED; addition of a new connector not exercised at baseline.
- **Approval:** Review Required

### SR-O.002 — Log rotation and retention

- **ID:** SR-O.002
- **Requirement:** The gateway shall write daily rolling log files and shall make the log retention behavior operator-controllable; the production retention value is `TBD`.
- **Rationale:** Unbounded log growth is an operational failure mode on long-lived hosts.
- **Source:** Serilog rolling file sink; docs/DEPLOYMENT.md.
- **Classification:** TO-BE (elaborates AS-IS behavior; retention configurability not evidenced).
- **Priority:** Low
- **Status:** Partial
- **Verification:** I
- **Evidence:** `src/RTSec.Kryptonian.Api/Program.cs` (RollingInterval.Day) CODE-OBSERVED; log retention settings not evidenced UNKNOWN.
- **Approval:** Review Required

### SR-O.003 — External credential renewal support

- **ID:** SR-O.003
- **Requirement:** The gateway and demonstration proxy shall support replacement of externally issued credentials (PFX) at runtime by atomic file replacement with fresh handshakes observing the new credential, and shall fail closed on invalid replacements.
- **Rationale:** Hospital PKI teams rotate credentials on their own schedule; the gateway must adopt rotated credentials without restart.
- **Source:** PR 48 (externally rotated PFX); docs/development.md.
- **Classification:** TO-BE (normative statement of AS-IS behavior; automated CA renewal itself not implemented).
- **Priority:** Medium
- **Status:** Existing
- **Verification:** T
- **Evidence:** docs/IMPLEMENTATION-TRACKER.md (fresh handshakes observing atomic PFX replacement; expired replacement fails closed) VERIFIED; docs/development.md DOCUMENTED.
- **Approval:** Review Required

### SR-O.004 — Metrics endpoint

- **ID:** SR-O.004
- **Requirement:** The gateway shall expose operational metrics (enrollment counts by profile/status, CA backend latency, request rates and errors) on a monitoring endpoint; the format (e.g., Prometheus) is `TBD` pending approval.
- **Rationale:** Renewal-scale estate operation needs trend visibility, not just live logs.
- **Source:** docs/ARCHITECTURE.md (Metrics (Future)).
- **Classification:** PROPOSED (planned/future in documentation).
- **Priority:** Low
- **Status:** Missing
- **Verification:** T
- **Evidence:** docs/ARCHITECTURE.md (Prometheus metrics planned) DOCUMENTED; no metrics endpoint in controllers CODE-OBSERVED.
- **Approval:** Review Required

### SR-O.005 — Dependency maintenance process

- **ID:** SR-O.005
- **Requirement:** The project shall apply compatible dependency security updates through reviewed pull requests with audit disclosure, and shall record which advisories remain as deployment blockers.
- **Rationale:** Security patching must be traceable and honest about residual risk.
- **Source:** PR 52 (compatible dependency security updates and audit disclosure).
- **Classification:** TO-BE (elaborates AS-IS process).
- **Priority:** Medium
- **Status:** Existing
- **Verification:** I
- **Evidence:** docs/IMPLEMENTATION-TRACKER.md (PR 52; MailKit 4.16.0 upgrade; remaining advisories listed) DOCUMENTED; `.github/workflows/` CODE-OBSERVED.
- **Approval:** Review Required

---

## 20. Failure and recovery requirements (SR-X)

### SR-X.001 — Unsupported revocation fails closed

- **ID:** SR-X.001
- **Requirement:** When a configured CA backend does not support revocation or CRL generation, the gateway shall reject the revocation operation with an explicit not-implemented error response and shall leave the certificate's status unchanged.
- **Rationale:** Falsely reporting success would leave operators trusting revoked credentials.
- **Source:** docs/IMPLEMENTATION-TRACKER.md ("Unsupported remote CA revocation fails closed"); KSPEC-002 UR-010 and §16.4.
- **Classification:** TO-BE (normative statement of AS-IS behavior).
- **Priority:** Critical
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Application/Services/CertificateRevocationService.cs` (501 on unsupported CRL generation; state unchanged) CODE-OBSERVED; docs/formal-specification/07-OPERATIONAL-RISK.md (fail-closed orientation) DOCUMENTED.
- **Approval:** Review Required

### SR-X.002 — Invalid replacement credentials fail closed

- **ID:** SR-X.002
- **Requirement:** The gateway and demonstration proxy shall reject externally replaced credentials that are invalid, expired, or untrusted and shall never keep invalid credentials as a fallback for new handshakes.
- **Rationale:** A stale or invalid credential silently accepted as fallback defeats the rotation's security intent.
- **Source:** PR 48; docs/development.md (invalid replacements fail closed; no stale credentials as fallback).
- **Classification:** TO-BE (normative statement of AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** T
- **Evidence:** docs/IMPLEMENTATION-TRACKER.md (expired replacement fails closed; unknown/expired/revoked client rejection) VERIFIED; `src/Kryptonian.DICOMTls/` CODE-OBSERVED.
- **Approval:** Review Required

### SR-X.003 — Issuer-mismatch and rotated-CA guards

- **ID:** SR-X.003
- **Requirement:** The gateway shall reject revocation or CRL operations that do not match the issuing CA of the target certificate (issuer mismatch or rotated-CA cases) with HTTP 409 rather than issuing a CRL or status change under the wrong issuer.
- **Rationale:** Cross-issuer revocation would corrupt the trust state of unrelated devices.
- **Source:** docs/formal-specification/07-OPERATIONAL-RISK.md (issuer/CA mismatch or rotated-CA guards reject with 409, CODE-OBSERVED).
- **Classification:** TO-BE (normative statement of AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Application/Services/CertificateRevocationService.cs` (issuer guards) CODE-OBSERVED; docs/formal-specification/07-OPERATIONAL-RISK.md DOCUMENTED.
- **Approval:** Review Required

### SR-X.004 — Unencrypted non-CRL requests refused

- **ID:** SR-X.004
- **Requirement:** When the CRL-over-HTTP listener is active, the gateway shall refuse any unencrypted request that is not a GET/HEAD CRL retrieval with HTTP 403, including administrative API calls.
- **Rationale:** The narrow HTTP exception must not become a general plaintext bypass.
- **Source:** Program.cs CrlOnlyHttp middleware; HTTP listener denial VERIFIED (tracker).
- **Classification:** TO-BE (normative statement of AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Program.cs` (403 for non-CRL on HTTP) CODE-OBSERVED; docs/IMPLEMENTATION-TRACKER.md (HTTP listener denies non-CRL requests) VERIFIED.
- **Approval:** Review Required

### SR-X.005 — One-time activation replay rejected

- **ID:** SR-X.005
- **Requirement:** The gateway shall reject a replayed activation code after it has been consumed, and shall reject bootstrap enrollment attempts from already-active devices that do not present a valid client certificate.
- **Rationale:** Replay is the primary attack on bootstrap credentials; executed evidence distinguishes the two cases explicitly.
- **Source:** docs/IMPLEMENTATION-TRACKER.md (one-time activation replay rejected); PR 42 (atomic one-time activation).
- **Classification:** TO-BE (normative statement of AS-IS behavior).
- **Priority:** Critical
- **Status:** Existing
- **Verification:** T
- **Evidence:** `artifacts/developer/verify-*/replay.txt` ("Public enrollment requires pending device activation; active devices must use authenticated re-enrollment") VERIFIED; `src/RTSec.Kryptonian.Api/Controllers/EstController.cs` CODE-OBSERVED.
- **Approval:** Review Required


### SR-X.006 — Database unavailability behavior

- **ID:** SR-X.006
- **Requirement:** The gateway shall respond to database unavailability with a distinguishable server error on affected requests, shall log the failure with its correlation ID, and shall report the database component as unhealthy on the health endpoint rather than crashing.
- **Rationale:** Storage loss is an operational certainty; degradation behavior determines whether the incident is diagnosable.
- **Source:** docs/formal-specification/07-OPERATIONAL-RISK.md (database outage scenarios); health-check design.
- **Classification:** TO-BE (elaborates AS-IS behavior).
- **Priority:** High
- **Status:** Existing
- **Verification:** T
- **Evidence:** `src/RTSec.Kryptonian.Api/Program.cs` (AddDbContextCheck) CODE-OBSERVED; `src/RTSec.Kryptonian.Api/Controllers/EstController.cs` (500 mapping with logging) CODE-OBSERVED.
- **Approval:** Review Required

### SR-X.007 — Backup and restore procedure

- **ID:** SR-X.007
- **Requirement:** The gateway shall provide a documented backup and restore procedure that recovers, in order, the Data Protection key ring, CA material with original passwords, and the database, and that restarts cleanly against the restored state.
- **Rationale:** Recovery order matters: a restored database without its key ring yields unreadable protected fields; a restored CA without its key cannot re-sign CRLs.
- **Source:** docs/formal-specification/07-OPERATIONAL-RISK.md (restore sequence: key ring first, then CA material, then database).
- **Classification:** TO-BE (procedure documented; automated backup/restore not implemented).
- **Priority:** High
- **Status:** Partial
- **Verification:** D
- **Evidence:** docs/formal-specification/07-OPERATIONAL-RISK.md (restore steps 1-4) DOCUMENTED; no automated backup mechanism in code CODE-OBSERVED.
- **Approval:** Review Required

---

## 21. Requirements summary and register

### 21.1 Counts

| Dimension | Value |
|---|---|
| Total requirements | 154 |
| By category | SR-F-REG 10, SR-F-EST 14, SR-F-CLM 9, SR-F-CAI 10, SR-F-ADM 10, SR-F-INT 8 (functional total 61), SR-S 14, SR-A 10, SR-Z 10, SR-C 10, SR-D 9, SR-P 5, SR-R 5, SR-V 4, SR-U 7, SR-B 7, SR-O 5, SR-X 7 |
| By status | Existing 108, Partial 25, Missing 18, Proposed 3 |
| By priority | Critical 36, High 71, Medium 38, Low 9 |
| Approval state | All records: Review Required (no approvals recorded or inferred) |

### 21.2 Requirements Register (canonical)

Every requirement in this document appears in the register below and vice versa.
This register is the canonical ID list consumed by KSPEC-008 (V&V) and KSPEC-009
(traceability). Verification abbreviations per KSPEC-000 §11.

| ID | Short title | Priority | Status | Verification |
|---|---|---|---|---|
| SR-F-REG.001 | Device registration with activation code | High | Existing | D |
| SR-F-REG.002 | Activation code one-time use, replay rejected | Critical | Existing | T |
| SR-F-REG.003 | Centralized device inventory | High | Existing | D |
| SR-F-REG.004 | Device record status lifecycle | High | Existing | T |
| SR-F-REG.005 | Enrollment identity binding | High | Existing | T |
| SR-F-REG.006 | Self-service requests create pending records only | High | Existing | T |
| SR-F-REG.007 | Anonymous endpoint abuse controls | Medium | Missing | T |
| SR-F-REG.008 | Trust removal blocks enrollment | Critical | Existing | T |
| SR-F-REG.009 | Device identity metadata preservation | High | Existing | D |
| SR-F-REG.010 | History preserved on device removal | Medium | Existing | I |
| SR-F-EST.001 | EST endpoint surface | Critical | Existing | T |
| SR-F-EST.002 | cacerts response format | High | Existing | T |
| SR-F-EST.003 | CSR acceptance and 64 KiB limit | High | Existing | T |
| SR-F-EST.004 | Successful enrollment response format | Critical | Existing | T |
| SR-F-EST.005 | Unknown profile label 404, no fallback | High | Existing | T |
| SR-F-EST.006 | Basic bootstrap authentication over TLS | Critical | Existing | T |
| SR-F-EST.007 | Legacy headers compatibility only | High | Partial | T |
| SR-F-EST.008 | Re-enroll requires native TLS client cert | Critical | Existing | T |
| SR-F-EST.009 | Client certificate validation on EST | Critical | Existing | T |
| SR-F-EST.010 | Pending 202 + Retry-After | High | Existing | T |
| SR-F-EST.011 | Deterministic pending semantics per backend | Medium | Missing | A |
| SR-F-EST.012 | Plain-HTTP EST refused (426) | Critical | Existing | T |
| SR-F-EST.013 | Distinguishable error semantics | High | Existing | I |
| SR-F-EST.014 | CSR attributes (csrattrs) endpoint | Low | Missing | T |
| SR-F-CLM.001 | Approval gates enrollment | Critical | Existing | T |
| SR-F-CLM.002 | Automatic renewal without re-approval | Critical | Existing | T |
| SR-F-CLM.003 | Renewal: new key, preserved identity | Critical | Existing | T |
| SR-F-CLM.004 | Certificate expiry visibility | High | Existing | D |
| SR-F-CLM.005 | Automated expiry scan and notification | High | Partial | T |
| SR-F-CLM.006 | Non-renewal alerting | High | Partial | D |
| SR-F-CLM.007 | Revocation with issuer-scoped signed CRLs | Critical | Existing | T |
| SR-F-CLM.008 | Transactional revocation persistence | High | Existing | T |
| SR-F-CLM.009 | Revoked devices denied renewal | Critical | Existing | T |
| SR-F-CAI.001 | Multi-backend configuration | High | Existing | D |
| SR-F-CAI.002 | Backend test + profile-bound routing | High | Existing | T |
| SR-F-CAI.003 | Pluggable connector abstraction | High | Existing | A |
| SR-F-CAI.004 | Transparent CA protocol mapping | Critical | Existing | A |
| SR-F-CAI.005 | ADCS SCEP endpoints configurable | Medium | Partial | I |
| SR-F-CAI.006 | EJBCA REST + honest revocation | Medium | Partial | I |
| SR-F-CAI.007 | ACME functional challenge set | Medium | Partial | T |
| SR-F-CAI.008 | CRL generation capability + fail closed | High | Existing | T |
| SR-F-CAI.009 | CA material persistence | High | Existing | T |
| SR-F-CAI.010 | Secure connector credential handling | High | Missing | I |
| SR-F-ADM.001 | Admin portal surface | High | Existing | D |
| SR-F-ADM.002 | Gateway dashboard summary | Medium | Existing | D |
| SR-F-ADM.003 | User lifecycle with lockout protection | High | Existing | T |
| SR-F-ADM.004 | API key lifecycle | High | Existing | T |
| SR-F-ADM.005 | Initial administrator bootstrap | High | Existing | T |

| SR-F-ADM.006 | Notification settings management | Medium | Existing | D |
| SR-F-ADM.007 | Profile management, additive trust updates | High | Existing | T |
| SR-F-ADM.008 | Gateway settings management | Medium | Existing | I |
| SR-F-ADM.009 | Live activity feed | Medium | Existing | D |
| SR-F-ADM.010 | Health and status endpoint | Medium | Existing | T |
| SR-F-INT.001 | Independent-client EST usability | High | Existing | T |
| SR-F-INT.002 | Compatibility headers never mandatory | High | Existing | I |
| SR-F-INT.003 | Authoritative OpenAPI admin contract | High | Missing | I |
| SR-F-INT.004 | EST profile schema currency | Medium | Partial | I |
| SR-F-INT.005 | DICOM mTLS demonstration | Medium | Existing | D |
| SR-F-INT.006 | Demo components carry no clinical claims | High | Existing | I |
| SR-F-INT.007 | No competing enrollment protocol | High | Existing | I |
| SR-F-INT.008 | Joint interoperability test vectors | Medium | Proposed | T |
| SR-S.001 | Transport security for credentials | Critical | Existing | T |
| SR-S.002 | CSR/returned-cert policy enforcement | Critical | Existing | T |
| SR-S.003 | Gateway-owned trust-policy gate | Critical | Existing | T |
| SR-S.004 | Forwarded-certificate guard | Critical | Existing | T |
| SR-S.005 | Authenticated forwarder identity | High | Missing | T |
| SR-S.006 | EST rate limiting | Medium | Existing | T |
| SR-S.007 | No hard-coded signing secrets | Critical | Missing | T |
| SR-S.008 | Secrets at rest | High | Missing | I |
| SR-S.009 | Password hashing (BCrypt ≥ 12) | Critical | Existing | I |
| SR-S.010 | Data Protection key ring management | High | Partial | T |
| SR-S.011 | Device private key non-archival | High | Proposed | I |
| SR-S.012 | Configurable CRL lifetime | High | Missing | T |
| SR-S.013 | CORS restriction | Medium | Existing | T |
| SR-S.014 | Dependency vulnerability management | High | Partial | T |
| SR-A.001 | Dual-scheme admin authentication | Critical | Existing | T |
| SR-A.002 | JWT token validation | Critical | Existing | T |
| SR-A.003 | API key storage and validation | Critical | Existing | T |
| SR-A.004 | Static compatibility API keys | Medium | Existing | I |
| SR-A.005 | API key carries generation-time role | Medium | Existing | D |
| SR-A.006 | JWT signing secret provisioning | Critical | Missing | T |
| SR-A.007 | EST bootstrap 401 challenge | Medium | Existing | T |
| SR-A.008 | Re-enroll credential rule | Critical | Existing | T |
| SR-A.009 | Development bypass confinement | High | Existing | T |
| SR-A.010 | Login failure without enumeration | Medium | Existing | T |
| SR-Z.001 | Role model (three roles) | Critical | Existing | T |
| SR-Z.002 | Device ops restricted to DeviceAdmin+ | Critical | Existing | T |
| SR-Z.003 | System config restricted to SystemAdmin | Critical | Existing | T |
| SR-Z.004 | Read-only access for Standard role | High | Existing | T |
| SR-Z.005 | Revocation restricted to DeviceAdmin+ | Critical | Existing | T |
| SR-Z.006 | API-key claims from record only | Critical | Existing | T |
| SR-Z.007 | Explicit role policy on all mutations | High | Partial | T |
| SR-Z.008 | Anonymous surface limited to documented set | High | Existing | I |
| SR-Z.009 | Confirmed self-service authorization model | Medium | Proposed | I |
| SR-Z.010 | No clinical capability derivation | Critical | Existing | I |
| SR-C.001 | Profile policy bundle enforced | Critical | Existing | I |
| SR-C.002 | Trust policy durability | High | Existing | T |
| SR-C.003 | Profile-configured validity (TBD policy) | High | Existing | I |
| SR-C.004 | Renewal window authorization | Critical | Existing | T |
| SR-C.005 | Issuer-scoped CRL publication | High | Existing | T |
| SR-C.006 | CRL distribution points on certificates | High | Existing | T |
| SR-C.007 | Online revocation checking at peers | High | Existing | D |
| SR-C.008 | Bounded revocation propagation semantics | Medium | Partial | A |
| SR-C.009 | Revocation vs. enrollment denial separation | High | Existing | I |
| SR-C.010 | Enrollment event per attempt | High | Partial | T |

| SR-D.001 | Durable persistence of all state | High | Existing | T |
| SR-D.002 | SQLite and PostgreSQL support | High | Partial | T |
| SR-D.003 | Additive schema upgrades | High | Existing | I |
| SR-D.004 | Upgrade safety boundary | High | Missing | T |
| SR-D.005 | UTC timestamp discipline | High | Existing | T |
| SR-D.006 | Lifecycle record retention (TBD period) | Medium | Partial | I |
| SR-D.007 | Data Protection key persistence | High | Partial | T |
| SR-D.008 | Sensitive field encryption at rest | High | Missing | I |
| SR-D.009 | Demo operations distinguishable | Low | Partial | I |
| SR-P.001 | EST rate-limit capacity | Medium | Partial | T |
| SR-P.002 | Enrollment request size bounds | Medium | Existing | T |
| SR-P.003 | Enrollment latency targets (TBD) | Low | Missing | A |
| SR-P.004 | Database connection pooling | Low | Existing | I |
| SR-P.005 | Profile/CA certificate caching | Low | Partial | I |
| SR-R.001 | Background watcher fault tolerance | High | Existing | T |
| SR-R.002 | Backend failure outcomes recorded | High | Existing | T |
| SR-R.003 | Notification delivery decoupled | Medium | Partial | T |
| SR-R.004 | Concurrency conflict handling | High | Existing | T |
| SR-R.005 | Graceful shutdown | Medium | Existing | I |
| SR-V.001 | Health-check availability | High | Existing | T |
| SR-V.002 | Component-level health reporting | Medium | Missing | T |
| SR-V.003 | Restart state preservation | Critical | Existing | T |
| SR-V.004 | Availability targets (TBD) | Medium | Missing | A |
| SR-U.001 | Audit record per trust decision | High | Partial | I |
| SR-U.002 | Blocked attempts recorded | High | Partial | T |
| SR-U.003 | Structured correlated logging | Medium | Existing | I |
| SR-U.004 | Administrative action logging | High | Partial | I |
| SR-U.005 | Audit tamper protection | Medium | Missing | A |
| SR-U.006 | Audit export capability | Low | Missing | I |
| SR-U.007 | Live log ring buffer bounds | Low | Existing | I |

| SR-B.001 | Deployment packaging options | Medium | Existing | I |
| SR-B.002 | Windows service and sandbox modes | Medium | Existing | T |
| SR-B.003 | External TLS certificate provisioning | High | Existing | I |
| SR-B.004 | Documented configuration settings | Medium | Existing | I |
| SR-B.005 | CRL-over-HTTP listener restriction | High | Existing | T |
| SR-B.006 | Upgrade preservation | Medium | Existing | D |
| SR-B.007 | Production secret provisioning | High | Partial | I |
| SR-O.001 | Extensible connector architecture | Medium | Existing | A |
| SR-O.002 | Log rotation and retention | Low | Partial | I |
| SR-O.003 | External credential renewal support | Medium | Existing | T |
| SR-O.004 | Metrics endpoint | Low | Missing | T |
| SR-O.005 | Dependency maintenance process | Medium | Existing | I |
| SR-X.001 | Unsupported revocation fails closed | Critical | Existing | T |
| SR-X.002 | Invalid replacements fail closed | High | Existing | T |
| SR-X.003 | Issuer-mismatch guards (409) | High | Existing | T |
| SR-X.004 | Unencrypted non-CRL requests refused | High | Existing | T |
| SR-X.005 | Activation replay rejected | Critical | Existing | T |
| SR-X.006 | Database unavailability behavior | High | Existing | T |
| SR-X.007 | Backup and restore procedure | High | Partial | D |

---

## 22. Traceability and open-item notes

- **Upstream**: every SR cites its KSPEC-002 UR in the Source field; governing
  constraints also cite KSPEC-000 §3/§4 and KSPEC-001 exclusions (EXC-001 – EXC-008).
- **Downstream**: KSPEC-004 (design), KSPEC-005 (interfaces), KSPEC-006
  (cybersecurity), KSPEC-007 (operational risk), KSPEC-008 (V&V), KSPEC-009
  (traceability), and KSPEC-010 (gap analysis) reference these SRs by ID only
  (KSPEC-000 §7) and shall not redefine them.
- **Unresolved thresholds** (`TBD` in requirements): SR-F-REG.007 (abuse-control
  threshold), SR-F-EST.011 (pending trigger matrix), SR-S.005 (forwarder
  authentication mechanism), SR-S.006 (production rate limits), SR-S.008
  (encryption approach), SR-S.011 (key-archival policy), SR-C.003 (certificate
  lifetime policy), SR-C.008 (stale-revocation policy), SR-D.006 (retention
  period), SR-P.001 (production capacity), SR-P.003 (latency budget), SR-R.003
  (delivery reliability), SR-V.004 (availability targets), SR-O.002 (log
  retention), SR-O.004 (metrics format), SR-F-CAI.010 (secrets mechanism);
  SR-A.006 inherits the ≥32-character recommendation pending confirmation.
- **Weak controls specified as TO-BE gaps** (status Missing or Partial): hard-coded
  JWT secret fallback (SR-S.007/SR-A.006), authentication-only backend test endpoint
  (SR-Z.007), anonymous endpoint without abuse controls (SR-F-REG.007, SR-Z.009),
  forwarder trust by source IP only (SR-S.005), hard-coded CRL lifetime (SR-S.012),
  DNS-01 challenge stub (SR-F-CAI.007), upgrade without transaction/backup boundary
  (SR-D.004), empty/stale OpenAPI contract files (SR-F-INT.003/004), health checks
  without connector components (SR-V.002), connector credentials in plain config
  (SR-F-CAI.010, SR-D.008).

---

*End of KSPEC-003, version 0.2 (Draft). All 154 requirements carry Approval
"Review Required"; no approval is recorded or inferred.*
