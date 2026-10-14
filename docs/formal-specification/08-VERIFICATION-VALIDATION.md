# Kryptonian Verification and Validation Specification

| Field | Value |
|---|---|
| Document title | Verification and Validation Specification (VT-*) |
| Document ID | KSPEC-008 |
| Version | 0.2 (Draft) |
| Review status | Draft — pending review |
| Created | 2026-10-13 |
| Baseline repository commit | `682f996c8447afec13bc1fce5b7f59e6a06efa25` (branch `feat/windows-sandbox-installer`, "Release Windows gateway and portable enrollment demo 0.1.4") |
| Repository | https://github.com/AAPM-RT-SEC/RTSec.Kryptonian |
| Revision history | 0.1 — 2026-10-13 — Initial draft: V&V strategy and 58 initial verification tests (VT-001 – VT-058) traced to the KSPEC-003 register; AS-IS coverage snapshot and executed-evidence mapping from docs/IMPLEMENTATION-TRACKER.md (Author: V&V engineering team). |
| Source references | KSPEC-000 (conventions; §4 classification, §5 provenance, §11 methods); KSPEC-002 (UR-001 – UR-038); KSPEC-003 (canonical register, 154 SRs); KSPEC-004 (architecture); KSPEC-005 (ICD-*); KSPEC-006 (THR-001 – THR-018); KSPEC-007 (ORK-001 – ORK-016); docs/IMPLEMENTATION-TRACKER.md; docs/development.md; docs/DEPLOYMENT.md; tests/ (8 test projects); artifacts/developer/verify-*/evidence.json; .github/workflows/ |
| Author | Name: TBD — Date: TBD — Signature: TBD |
| Reviewer | Name: TBD — Date: TBD — Signature: TBD |
| Approver | Name: TBD — Date: TBD — Signature: TBD |
| Reconciliation revision | 0.2 - 2026-10-13 - source/document audit and publication controls (KSPEC-016) |

**Product classification (binding, KSPEC-000 §3).** Kryptonian is enterprise healthcare IT
cybersecurity infrastructure: an EST (RFC 7030) certificate enrollment, identity
provisioning, and credential lifecycle gateway. It is **not a regulated medical device**
and performs no clinical functions. No verification activity, test result, or acceptance
criterion in this document may be read as claiming FDA classification, IEC 62304 lifecycle
classification, ISO 14971 hazard coverage, SDC conformity, an adopted EST profile, DICOM
B.12/B.13 certification, or any other standards-conformance status. Verification verifies
**device identity, enrollment authorization, network trust, and application-layer
authorization only**; no gateway state derives clinical capability.

**Evidence discipline (binding, KSPEC-000 §5 and §13).** Every VT carries an **Evidence
Status** field. Only tests whose outcome is recorded in `docs/IMPLEMENTATION-TRACKER.md`
or in executed-evidence artifacts under `artifacts/developer/verify-*/` may be marked as
executed, and then only with the exact statement the tracker records. All other VTs are
marked **Proposed** and must never be presented as executed. No conformity claims are
made or implied by any test passing.

---

## 1. Introduction

### 1.1 Purpose

This document defines how the RTSec.Kryptonian gateway is verified and validated,
consistent with ISO/IEC/IEEE 29148 terminology and the KSPEC-000 conventions: it states
the verification and validation objectives, the test environments, the test levels, and
a test specification of verification tests (VT-nnn) traced to the canonical requirements
register of KSPEC-003 (154 SR-* requirements), the threat register of KSPEC-006
(THR-nnn), and the operational risk register of KSPEC-007 (ORK-nnn).

### 1.2 Scope

Verification: "confirmation, through the provision of objective evidence, that specified
requirements have been fulfilled" (ISO/IEC/IEEE 29148 usage; method codes T/I/A/D per
KSPEC-000 §11). Validation: confirmation that the gateway, as verified, meets the
stakeholder needs of KSPEC-002 (UR-001 – UR-038) in its intended, explicitly
non-clinical operational context. This document covers the gateway
(`RTSec.Kryptonian.{Api,Application,Domain,Infrastructure,Web,Ui}`) and the
demonstration/reference clients (`Kryptonian.MedicalDevice`, `Kryptonian.DICOMTls`,
`RTSec.Kryptonian.DimseTlsProxy`, `RTSec.Kryptonian.CaHarness`) — the latter verified as
demonstration components only, never as product deliverables for production
(KSPEC-000 §3).

### 1.3 Definitions

Defined terms follow KSPEC-000 §9 (device identity, certificate enrollment authorization,
network trust, application-level authorization, clinical capability — out of scope —,
profile, CA backend, connector, trust policy, enrollment event, CRL, renewal window,
admin portal, DIMSE-TLS proxy). Additional terms:

| Term | Meaning |
|---|---|
| AS-IS test | An automated test present in `tests/` at the baseline commit. |
| Executed local evidence | A result recorded by the developer verifier and summarized in `docs/IMPLEMENTATION-TRACKER.md`, with artifacts under `artifacts/developer/verify-*/`. |
| Proposed test | A test specified in this document that has not been implemented or executed. |
| Test CA harness | The repository's stand-in CA backend (`RTSec.Kryptonian.CaHarness` and the self-signed/ACME connectors) used for repeatable local verification. |

---

## 2. Verification and validation objectives

VO-1 — **Protocol correctness.** Demonstrate that the device-facing EST surface behaves
per KSPEC-005 ICD-EST.* and the RFC 7030 usage specified in KSPEC-003 (SR-F-EST area),
without fallback behaviors that could silently enroll a device under the wrong policy
(SR-F-EST.005, SR-F-INT.007).

VO-2 — **Enrollment authorization integrity.** Demonstrate that only approved device
records can obtain credentials, that activation bootstrap secrets are single-use and
replay-rejected, and that enrolled identities are bound to the pending registration
(SR-F-REG, SR-F-CLM.001, SR-A.008).

VO-3 — **Credential lifecycle correctness.** Demonstrate approval-gated issuance,
renewal within the renewal window with a fresh key and preserved identity, and
revocation with issuer-scoped signed CRLs and fail-closed semantics (SR-F-CLM, SR-C,
SR-X.001).

VO-4 — **Network trust enforcement.** Demonstrate TLS/mTLS peer authentication, client
certificate validation (issuer, EKU/purpose, online revocation), and rejection of
forwarded-certificate spoofing (SR-S.001/003/004, SR-F-EST.008/009, SR-C.007).

VO-5 — **Authentication and authorization.** Demonstrate the three-role RBAC model,
dual-scheme administrator authentication, API-key claim binding, and the documented
anonymous surface (SR-A, SR-Z).

VO-6 — **CA backend interoperability.** Demonstrate multi-backend configuration,
test-before-activate routing, transparent protocol mapping, and honest failure reporting
for each connector class; remote ADCS/EJBCA interoperability is explicitly
**unverified** at baseline (SR-F-CAI).

VO-7 — **Data durability and recovery.** Demonstrate durable persistence across restart,
UTC timestamp discipline, additive schema upgrades, and fail-safe behavior when the
database is unavailable (SR-D, SR-V.003, SR-X.006, SR-R).

VO-8 — **Operational robustness.** Demonstrate rate-limit capacity, request size bounds,
graceful shutdown, health/status reporting, and deployment acceptance for the supported
packaging options (SR-P, SR-V, SR-B, SR-O).

VO-9 — **Security threat mitigation.** Provide test evidence against the KSPEC-006
threat register (THR-001 – THR-018) for the controls classified C (code) or V
(verifiable), without overclaiming the boundary limitations recorded there.

VO-10 — **Operational risk control verification.** Provide test evidence for the KSPEC-007
risk controls (ORK-001 – ORK-016) that specify T-method verification, particularly the
Critical-rated ORK-006, ORK-008, ORK-009, ORK-011, ORK-012, ORK-014, ORK-015.

VO-11 — **Validation against stakeholder needs.** Confirm through the UR → SR → VT chain
(KSPEC-009) that each KSPEC-002 stakeholder need is either verified, partially verified,
or recorded as an open item — never silently dropped.

VO-12 — **Honest boundary maintenance.** Verify the negative claims that bound the
product: no clinical capability derivation (SR-Z.010), no competing enrollment protocol
(SR-F-INT.007), demo components carry no clinical claims (SR-F-INT.006), and
compatibility headers never become mandatory (SR-F-INT.002).

---

## 3. Verification and validation strategy

### 3.1 Method mix

Verification methods per KSPEC-000 §11: **T** = Test, **I** = Inspection, **A** =
Analysis, **D** = Demonstration. Each SR's method is fixed by KSPEC-003 §21 and shall
not be reclassified here. VTs realize T-method and D-method requirements; I/A-method
requirements are verified by inspection/analysis records referenced from the VT
evidence status where a VT exists for them.

### 3.2 Strategy principles

- **Live-boundary-first.** Baseline live checks exposed two defects that controller-mocked
  tests missed (Schannel rejection of ephemeral attached RSA keys; SQLite `DateTime.Kind`
  shifting refreshed CRL revocation times five hours on one host). Both were fixed with
  runnable regressions (tracker). Verification therefore requires at least one
  real-network-boundary exercise (real HTTPS listener, real mTLS handshake, real
  Schannel/X509Chain evaluation) per security-critical requirement area, per UR-036.
- **Test CA harness instead of hospital CAs.** Local verification uses the repository CA
  harness (self-signed in-process CA, ACME via Certes, `RTSec.Kryptonian.CaHarness`
  stand-in backend). Hospital ADCS/EJBCA interoperability remains a Proposed,
  separately-scheduled activity (VT-043); local results never imply hospital-CA
  interoperability.
- **Regression discipline.** Every defect found by verification yields a runnable
  regression test in the matching `tests/` project. CI (Windows GitHub runners) builds
  the UI, tests the solution, and builds the standalone proxy; high/critical UI
  dependency advisories fail the build (tracker).
- **Fail-closed orientation.** For every failure-injection test the expected result is
  rejection with a distinguishable error and **no** partial or intermediate trust state
  (SR-R.002, SR-X.001/002; KSPEC-007 fail-closed orientation).
- **No fabricated evidence.** A VT is marked executed only against recorded evidence
  (§6.1 evidence-status classes). Evidence artifacts are reproducible via the developer
  verifier (docs/development.md), never via screenshots.
- **Known skips.** Eighteen pre-existing Windows infrastructure test skips remain at
  baseline (tracker). Any VT whose environment depends on a skipped capability is marked
  Proposed and must record the skip dependency when implemented.

### 3.3 Pass/fail and traceability rules

A VT passes when every acceptance criterion in its record is met on the specified
environment with the specified evidence class. Any deviation is recorded as a finding
and feeds KSPEC-010 (gap analysis); a deviation on a Critical-priority SR blocks any
review-approval recommendation for that SR (KSPEC-000 §8: approval is never inferred
from implementation). KSPEC-009 consumes the VT → SR links of this document; VT IDs
must not be redefined there or anywhere else (KSPEC-000 §7).

---

## 4. Test environment setup

### 4.1 Environments

| Env | Name | Description | Used for |
|---|---|---|---|
| E1 | Local developer system | Windows developer host, .NET 8 SDK; `dotnet test` over the solution; loopback HTTPS listeners (distinct localhost admin/EST/CRL ports in sandbox mode); Windows Schannel and `X509Chain` for real OS-level PKI evaluation | Unit, integration, negative, concurrency, and the executed developer-verifier evidence (tracker) |
| E2 | Container environment | `docker-compose*.yml` stack from `deploy/` (API container; PostgreSQL option per docs/DEPLOYMENT.md) | Deployment acceptance (VT-056), PostgreSQL parity (VT-052) |
| E3 | Test CA harness | `RTSec.Kryptonian.CaHarness` stand-in CA + `SelfSignedEstCaConnector` (in-process CA with CRL generation) + `AcmeCaConnector`/challenge stores pointed at the loopback ACME challenge endpoint | EST conformance, enrollment, revocation, ACME tests without a hospital CA |
| E4 | CI | Windows GitHub Actions (`.github/workflows/`): UI build, solution tests, standalone proxy build, UI `npm audit` high/critical gate | Regression gate (VT-058) |

### 4.2 Test data and fixtures

- Test CA harness profile with subject-DN rules, key usage, validity, and trust policy
  per KSPEC-003 SR-C.001; synthetic device records (pending/active/untrusted/retired);
  one-time activation codes (valid, expired, already-consumed); CSRs (valid, malformed,
  oversize >64 KiB, wrong subject, disallowed key usage/EKU); an issuer-scoped CRL
  produced by the self-signed CA.
- Synthetic demo records remain distinguishable from real ones (SR-D.009); no real
  hospital or patient data is used in any environment. No clinical deployment and no
  external partner communication are involved in any verification activity.

### 4.3 Executed-evidence artifact structure

Executed local evidence is recorded under `artifacts/developer/verify-*/evidence.json`.
The artifact recorded at baseline (run `verify-fa867498c35c4ca7bf58c0d4bc3acd21`,
2026-09-08 UTC) contains: `issuerFingerprint` (fingerprint of the issuing CA), `utc`,
`deviceId`, `certificateId`, `revocationChecked: true`, `crlUrl`, and a `run`
identifier [VERIFIED — file inspected]. The tracker additionally records the two
received SOP Instance UIDs of the executed DICOM C-STORE transfers
(`2.25.154518358314734480382348393848679450333` and
`2.25.281306672341327719638925984637794989584`). Combined live-run evidence is local
and ignored by Git; VTs citing it must instruct the reader to reproduce via the
developer verifier rather than rely on a stored artifact or screenshot.

---

## 5. Test levels

| Level | Description | Primary environments | Example VTs |
|---|---|---|---|
| Unit | Component-level tests of domain entities, services, crypto/PKCS, persistence, connectors | E1 | VT-030, VT-041, VT-051 |
| Integration | Gateway exercised through real HTTP/HTTPS listeners with the test CA harness, real authentication handlers, real persistence | E1, E3 | VT-001 – VT-010, VT-021 – VT-027, VT-049 |
| EST protocol conformance | RFC 7030 operation-by-operation checks against ICD-EST.*: cacerts, simpleenroll, simplereenroll, status codes, content types, profile-scoped paths | E1, E3 | VT-001 – VT-013 |
| Security | Threat-control tests mapped to THR-001 – THR-018: trust-policy gate, forwarding guard, secrets, CORS, revocation trust | E1, E3 | VT-031 – VT-036, VT-046 |
| Authentication | JWT/API-key/Basic schemes, 401 challenges, token validation, login non-enumeration | E1 | VT-006, VT-037, VT-039 |
| Authorization | RBAC role matrix over all mutating and read endpoints | E1 | VT-038, VT-039 |
| CA interoperability | Connector-level behavior per backend class; remote ADCS/EJBCA left explicitly unverified | E1, E3 (E2 for remote labs, proposed) | VT-040 – VT-043 |
| Certificate renewal | Renewal-window authorization, fresh-key rotation, identity preservation, external credential renewal | E1 | VT-022 – VT-024 |
| Revocation | Issuer-scoped signed CRL publication, transactional persistence, online checking at peers, fail-closed unsupported backends | E1, E3 | VT-025 – VT-028, VT-041 |
| DICOM TLS interoperability | Demonstration-level mTLS DIMSE association, C-STORE transfer, SOP UID comparison, proxy trust lifecycle | E1 | VT-046, VT-047 |
| Negative | Malformed/oversized/wrongly-authenticated requests must be rejected with distinguishable errors and no state change | E1, E3 | VT-020, VT-044 |
| Fault injection | Backend failure, DB unavailability, invalid replacement credentials, watcher faults | E1, E3 | VT-045, VT-046, VT-029 |
| Concurrency | Simultaneous activation consumption, concurrent revocation conflicts | E1 | VT-026, VT-048 |
| Performance | Rate-limit capacity, request size bounds; latency targets remain TBD (SR-P.003) | E1 | VT-055 |
| Database persistence | Restart preservation, timestamp discipline, schema upgrades, provider parity | E1, E2 | VT-049 – VT-052 |
| Recovery | Backup/restore, graceful shutdown | E1, E2 | VT-053, VT-054 |
| Deployment acceptance | docker-compose stack, Windows service/sandbox modes, CI regression gate | E2, E1, E4 | VT-056 – VT-058 |

---

## 6. Test specification

### 6.1 Evidence-status classes

| Status | Meaning |
|---|---|
| Covered — executed local evidence (VERIFIED) | The tracker records the exact result quoted in the VT; reproduce via the developer verifier (docs/development.md). |
| Covered — AS-IS automated test | A test exists in `tests/` at the baseline commit (assertions CODE-OBSERVED) and is executed by CI per the tracker; no separate live-run claim is made. |
| Partially covered | An AS-IS test or tracker evidence covers part of the VT; the remainder is Proposed. |
| Proposed | Specified here only; not implemented or executed. Never presented as executed. |

Each VT record uses the fields: Test ID, Linked requirements, Linked THR/ORK, Level,
Preconditions, Test inputs, Procedure, Expected results, Acceptance criteria, Evidence
status.

### 6.2 EST protocol conformance tests (VT-001 – VT-013)

#### VT-001 — EST endpoint surface and profile-scoped paths

| Field | Value |
|---|---|
| Test ID | VT-001 |
| Linked requirements | SR-F-EST.001 |
| Linked THR/ORK | THR-002 |
| Level | Integration (EST conformance) |
| Preconditions | Gateway running on E1/E3 with a test profile labeled `test-profile` |
| Test inputs | HTTP(S) requests to `/.well-known/est/test-profile/cacerts`, `/simpleenroll`, `/simplereenroll` with correct methods |
| Procedure | 1. Request each EST operation under the profile-scoped path. 2. Repeat with a second configured profile. 3. Inspect routing for default-profile fallthrough. |
| Expected results | Each operation responds on every configured profile path; responses match ICD-EST.001. |
| Acceptance criteria | All documented operations reachable on all profiles; no operation silently rerouted to another profile. |
| Evidence status | Covered — AS-IS automated test: `tests/RTSec.Kryptonian.Api.Tests/Integration/EstRouteTests.cs` and `EstIntegrationTests.cs` (assertions CODE-OBSERVED; CI execution per tracker PR 40/49). |

#### VT-002 — cacerts operation returns the profile trust set

| Field | Value |
|---|---|
| Test ID | VT-002 |
| Linked requirements | SR-F-EST.002 |
| Linked THR/ORK | THR-012 |
| Level | Integration (EST conformance) |
| Preconditions | Profile configured with one or more CA certificates (E3 harness CA) |
| Test inputs | `GET /.well-known/est/{label}/cacerts` |
| Procedure | 1. Issue GET. 2. Decode the PKCS#7 body. 3. Compare returned certificates with the profile trust set. |
| Expected results | Response contains exactly the profile's CA certificates in the ICD-EST.003 content type. |
| Acceptance criteria | Returned set equals configured set; content type correct. |
| Evidence status | Covered — AS-IS automated test: `tests/RTSec.Kryptonian.Api.Tests/Controllers/EstControllerTests.cs` (assertions CODE-OBSERVED). |

#### VT-003 — CSR acceptance and 64 KiB limit

| Field | Value |
|---|---|
| Test ID | VT-003 |
| Linked requirements | SR-F-EST.003, SR-P.002 |
| Linked THR/ORK | THR-004, THR-013 |
| Level | Integration (negative boundary) |
| Preconditions | Approved, activated device record; bootstrap credentials available |
| Test inputs | Valid CSR; CSR of just over 64 KiB; empty body |
| Procedure | 1. POST valid CSR to `/simpleenroll` → expect enrollment success. 2. POST oversize CSR → expect rejection. 3. POST empty body → expect distinguishable error. |
| Expected results | Valid CSR accepted; oversize and empty rejected before CA contact. |
| Acceptance criteria | 64 KiB boundary enforced; enrollment events for rejected requests show no CA backend outcome. |
| Evidence status | Covered — AS-IS automated test: `EstControllerTests.cs` (limit CODE-OBSERVED); live-boundary oversize case Proposed. |


#### VT-004 — Successful enrollment response format

| Field | Value |
|---|---|
| Test ID | VT-004 |
| Linked requirements | SR-F-EST.004 |
| Linked THR/ORK | THR-005 |
| Level | Integration (EST conformance) |
| Preconditions | Approved device; bootstrap or re-enroll credentials valid; E3 self-signed backend active |
| Test inputs | Valid CSR POST to `/simpleenroll` |
| Procedure | 1. POST CSR. 2. Inspect status, content type, and body. 3. Parse the returned certificate and compare subject/key usage/EKU/validity against the profile policy. |
| Expected results | HTTP 200 with the issued certificate in the PKCS#7 response format of ICD-EST.003; certificate matches profile policy. |
| Acceptance criteria | Issued certificate fields equal the profile policy bundle (SR-C.001); no private headers required (SR-F-INT.001). |
| Evidence status | Covered — AS-IS automated test: `EstControllerTests.cs`, `EstIntegrationTests.cs` (assertions CODE-OBSERVED); live-boundary curl case executed per tracker (see VT-046). |

#### VT-005 — Unknown profile label rejected (404, no fallback)

| Field | Value |
|---|---|
| Test ID | VT-005 |
| Linked requirements | SR-F-EST.005 |
| Linked THR/ORK | THR-012 |
| Level | Integration (EST conformance / negative) |
| Preconditions | Gateway with profile `test-profile` configured |
| Test inputs | Requests to `/.well-known/est/unknown-label/...` and to a disabled profile |
| Procedure | 1. Request cacerts and simpleenroll under an unknown label. 2. Repeat with a disabled label. 3. Confirm no enrollment event is created for the known profile. |
| Expected results | HTTP 404 for unknown/disabled labels; no fallback to any default profile. |
| Acceptance criteria | No certificate issued; no cross-profile state change; distinguishable error per SR-F-EST.013. |
| Evidence status | Covered — AS-IS automated test: `EstRouteTests.cs` (PR 37 regression, CODE-OBSERVED); routing behavior VERIFIED per tracker ("Profile issuer routing and unknown-label rejection", PR 37). |

#### VT-006 — Basic bootstrap authentication over TLS (401 challenge)

| Field | Value |
|---|---|
| Test ID | VT-006 |
| Linked requirements | SR-F-EST.006, SR-A.007 |
| Linked THR/ORK | THR-003; ORK-005 |
| Level | Integration (EST conformance / authentication) |
| Preconditions | Pending device with one-time activation code; HTTPS EST listener; no private headers |
| Test inputs | curl with HTTP Basic (activation credentials) over real HTTPS; same request over plain HTTP; request with wrong password; request without credentials |
| Procedure | 1. Enroll via independent curl over real HTTPS using standard Basic auth only. 2. Attempt plain HTTP. 3. Attempt unauthenticated/wrong-credential request. |
| Expected results | Step 1 succeeds with no private enrollment headers; step 3 yields HTTP 401 with challenge; plain HTTP refused (VT-010). |
| Acceptance criteria | Standard-client usability proven at the live boundary; 401 challenge present. |
| Evidence status | Covered — executed local evidence (VERIFIED): tracker records "Independent curl HTTP Basic EST over real HTTPS; no private enrollment headers." and PR 42 (standard Basic EST bootstrap). |

#### VT-007 — Re-enroll requires a native TLS client certificate

| Field | Value |
|---|---|
| Test ID | VT-007 |
| Linked requirements | SR-F-EST.008, SR-A.008 |
| Linked THR/ORK | THR-006 |
| Level | Integration (EST conformance / renewal) |
| Preconditions | Device holds a previously issued certificate; renewal window behavior per profile (VT-024) |
| Test inputs | `POST /simplereenroll` with the valid client certificate; same request without a client certificate; with an untrusted client certificate |
| Procedure | 1. Re-enroll with the native TLS client certificate. 2. Repeat without it. 3. Repeat with an untrusted certificate. |
| Expected results | Step 1 succeeds; steps 2–3 rejected with distinguishable errors; activation codes are never accepted for re-enroll. |
| Acceptance criteria | Re-enroll authenticated solely by the TLS client certificate; rejected requests create denied-enrollment events. |
| Evidence status | Covered — executed local evidence (VERIFIED): tracker records "renewal uses a native TLS client certificate, generates a new key and preserves approved identity"; AS-IS tests: `EstEnrollmentClientRenewalTests.cs` (CODE-OBSERVED). |


#### VT-008 — EST client-certificate validation (issuer, EKU, revocation)

| Field | Value |
|---|---|
| Test ID | VT-008 |
| Linked requirements | SR-F-EST.009 |
| Linked THR/ORK | THR-001; ORK-008 |
| Level | Integration (security) |
| Preconditions | EST listener with `ConfiguredClientCertificateTrust` over `Kryptonian:Tls:ClientCaFiles`; E3 CA with CRL published |
| Test inputs | Client certificate chains: issued by configured CA (valid); wrong issuer; wrong/missing TLS-client EKU; revoked |
| Procedure | 1. Handshake with each chain against the EST listener. 2. Attempt re-enroll with each. 3. Inspect rejection reasons and events. |
| Expected results | Only the valid chain is accepted; wrong-issuer, wrong-EKU, and revoked chains rejected. |
| Acceptance criteria | Issuer, purpose, and online revocation each independently enforced. |
| Evidence status | Covered — AS-IS automated test: `tests/RTSec.Kryptonian.Api.Tests/Authentication/ConfiguredClientCertificateTrustTests.cs` (CODE-OBSERVED); tracker records "Developer trust adds a separate issuer/hostname/purpose regression." |

#### VT-009 — Pending enrollment: 202 + Retry-After

| Field | Value |
|---|---|
| Test ID | VT-009 |
| Linked requirements | SR-F-EST.010 |
| Linked THR/ORK | — |
| Level | Integration (EST conformance) |
| Preconditions | Device whose CA backend defers issuance (E3 harness pending mode) |
| Test inputs | `/simpleenroll` requests while issuance is deferred; a later request after issuance completes |
| Procedure | 1. POST CSR during deferral → observe 202. 2. Read `Retry-After`. 3. Repeat after the backend completes → observe final result. |
| Expected results | HTTP 202 with a valid `Retry-After` while pending; eventual success or distinguishable failure afterward. |
| Acceptance criteria | Pending response never fabricates success; Retry-After present and sane. |
| Evidence status | Covered — AS-IS automated test: `EstControllerTests.cs`; client behavior: `tests/Kryptonian.MedicalDevice.Enrollment.Tests/EstEnrollmentClientPendingTests.cs` (both CODE-OBSERVED). |

#### VT-010 — Plain-HTTP EST refused (426); CRL-only HTTP listener

| Field | Value |
|---|---|
| Test ID | VT-010 |
| Linked requirements | SR-F-EST.012, SR-B.005 |
| Linked THR/ORK | THR-013 |
| Level | Integration (negative / deployment security) |
| Preconditions | Gateway with default listeners (plain-HTTP escape hatch disabled) |
| Test inputs | Plain-HTTP request to an EST path; to the CRL path; to an admin path |
| Procedure | 1. Send each request over plain HTTP. 2. Record status codes. 3. Repeat with `Kryptonian:Est:AllowPlainHttp` set and confirm the deviation is explicit and development-scoped. |
| Expected results | EST and admin paths refused over plain HTTP (HTTP 426 for EST); only CRL distribution is served over the HTTP listener. |
| Acceptance criteria | No credential-bearing exchange accepted unencrypted; deviation path documented and non-default. |
| Evidence status | Covered — executed local evidence (VERIFIED): tracker records "HTTP listener denies non-CRL requests"; AS-IS test: `EstRouteTests.cs` (CODE-OBSERVED). |


#### VT-011 — Legacy compatibility headers remain optional

| Field | Value |
|---|---|
| Test ID | VT-011 |
| Linked requirements | SR-F-EST.007, SR-F-INT.002, SR-F-INT.007 |
| Linked THR/ORK | — |
| Level | Inspection + Demonstration |
| Preconditions | Gateway at baseline; sample device client with legacy activation headers |
| Test inputs | Enrollment with and without legacy headers; documentation audit of header naming |
| Procedure | 1. Enroll without legacy headers (standard path only) → succeeds. 2. Enroll with legacy headers (sample client) → succeeds for compatibility. 3. Inspect docs/spec for any statement making legacy headers mandatory. |
| Expected results | Legacy headers accepted for compatibility only; no requirement or interop claim depends on them. |
| Acceptance criteria | No spec text or enforced validation requires legacy headers (KSPEC-002 UR-019 boundary). |
| Evidence status | Partially covered — standard-path independence VERIFIED per tracker ("no private enrollment headers"); legacy-header compatibility inspection Proposed (SR-F-EST.007 status Partial). |

#### VT-012 — Distinguishable error semantics and deterministic pending

| Field | Value |
|---|---|
| Test ID | VT-012 |
| Linked requirements | SR-F-EST.013, SR-F-EST.011 (Missing) |
| Linked THR/ORK | — |
| Level | Integration + Analysis |
| Preconditions | Matrix of invalid scenarios prepared (unknown profile, bad credentials, unauthorized renewal, issuer-mismatch CRL ops) |
| Test inputs | One request per invalid scenario class |
| Procedure | 1. Execute each scenario. 2. Record (status, error code, event reason). 3. Analyze pending semantics per backend type for determinism (SR-F-EST.011). |
| Expected results | Each scenario yields a distinct, diagnosable outcome; pending behavior deterministic per configured backend. |
| Acceptance criteria | Error taxonomy matches ICD-ERR.001; no two distinct failure classes share indistinguishable responses. |
| Evidence status | Proposed — SR-F-EST.011 is Missing at baseline; SR-F-EST.013 has Inspection-level support via existing error paths (CODE-OBSERVED) but no assertion test exists. |

#### VT-013 — CSR attributes (`csrattrs`) endpoint

| Field | Value |
|---|---|
| Test ID | VT-013 |
| Linked requirements | SR-F-EST.014 (Missing) |
| Linked THR/ORK | THR-004 |
| Level | Integration (EST conformance) — TO-BE |
| Preconditions | TO-BE implementation of `/.well-known/est/{label}/csrattrs` exists |
| Test inputs | GET csrattrs; parse the attributes OID set |
| Procedure | 1. GET csrattrs. 2. Verify content type and encoding. 3. Verify advertised attributes match the profile's subject/EKU requirements. |
| Expected results | Correctly encoded attributes response consistent with the profile. |
| Acceptance criteria | Devices can construct conforming CSRs from csrattrs alone. |
| Evidence status | Proposed — requirement Missing at baseline (KSPEC-003 register). |

### 6.3 Device registration and identity tests (VT-014 – VT-020)

#### VT-014 — Device registration and activation code issuance

| Field | Value |
|---|---|
| Test ID | VT-014 |
| Linked requirements | SR-F-REG.001, SR-F-REG.009, SR-F-REG.010, SR-F-REG.003 |
| Linked THR/ORK | THR-003 |
| Level | Integration + Demonstration |
| Preconditions | DeviceAdmin actor authenticated on E1/E3 |
| Test inputs | Administrative alias; device metadata (serial number); removal operation on an enrolled device |
| Procedure | 1. Register device by alias only → obtain one-time activation code. 2. Verify inventory entry (status, profile, identity metadata). 3. Enroll, then remove the device and inspect history retention. |
| Expected results | Registration needs no manual certificate identity entry; code issued; inventory and history correct. |
| Acceptance criteria | Device identity metadata preserved from CSR; removal retains history per SR-F-REG.010. |
| Evidence status | Covered — AS-IS automated test: `tests/RTSec.Kryptonian.Application.Tests/Services/DeviceServiceActivationTests.cs` (CODE-OBSERVED); admin portal demonstration per tracker's twenty device enrollment/lifecycle tests. |

#### VT-015 — Activation code single-use and replay rejection

| Field | Value |
|---|---|
| Test ID | VT-015 |
| Linked requirements | SR-F-REG.002, SR-X.005 |
| Linked THR/ORK | THR-003, THR-006; ORK-005 |
| Level | Integration (negative / security) |
| Preconditions | Valid unused activation code captured for replay |
| Test inputs | First use of the code; replayed identical request; expired code; already-consumed code |
| Procedure | 1. Enroll with the code → success. 2. Replay the exact request → rejection. 3. Attempt expired/consumed codes → rejection. 4. Inspect code storage (hashed, bounded lifetime). |
| Expected results | Replay and reuse rejected with distinguishable errors; code consumed atomically on validated bootstrap acceptance before CA issuance. |
| Acceptance criteria | One-time semantics hold under replay and race (see VT-048); codes stored hashed. |
| Evidence status | Covered — executed local evidence (VERIFIED): tracker records "One-time activation replay rejected". AS-IS tests: `DeviceServiceActivationTests.cs`, `tests/RTSec.Kryptonian.Infrastructure.Tests/Data/DeviceActivationConcurrencyTests.cs` (CODE-OBSERVED). |


#### VT-016 — Enrollment identity binding

| Field | Value |
|---|---|
| Test ID | VT-016 |
| Linked requirements | SR-F-REG.005 |
| Linked THR/ORK | THR-001 |
| Level | Integration (security) |
| Preconditions | Pending device registration with expected subject rules from the profile |
| Test inputs | CSR with CN matching the pending registration; CSR with a CN already registered; CSR without a serial number |
| Procedure | 1. Enroll with a matching, unique CN → success. 2. Enroll with a duplicate CN → rejection. 3. Enroll without required metadata → rejection. 4. Inspect the issued certificate subject against the pending record. |
| Expected results | Issued identity bound to the approved registration; duplicates and incomplete metadata rejected. |
| Acceptance criteria | No enrollment where the CSR identity diverges from the approved record (PR 38 behavior). |
| Evidence status | Covered — executed local evidence (VERIFIED): tracker records PR 38 "Authenticated enrollment identity binding"; AS-IS test: `DeviceServiceActivationTests.cs` (CODE-OBSERVED). |

#### VT-017 — Self-service requests create pending records only

| Field | Value |
|---|---|
| Test ID | VT-017 |
| Linked requirements | SR-F-REG.006 |
| Linked THR/ORK | THR-002 |
| Level | Integration |
| Preconditions | Standard (non-DeviceAdmin) actor authenticated |
| Test inputs | Self-service device request via the permitted surface |
| Procedure | 1. Submit a self-service request. 2. Attempt enrollment against the resulting record before approval. |
| Expected results | Record created with pending status; enrollment denied until an administrator approves. |
| Acceptance criteria | No credential issues from a self-created record; approval gate interlocks (VT-021). |
| Evidence status | Covered — AS-IS automated test: `DeviceServiceActivationTests.cs` (pending-state assertions CODE-OBSERVED). |

#### VT-018 — Trust removal blocks enrollment and renewal

| Field | Value |
|---|---|
| Test ID | VT-018 |
| Linked requirements | SR-F-REG.008 |
| Linked THR/ORK | THR-012; ORK-005 |
| Level | Integration (security) |
| Preconditions | Active enrolled device; DeviceAdmin actor |
| Test inputs | Trust-removal action; subsequent enroll and re-enroll attempts by the device |
| Procedure | 1. Remove trust for the device. 2. Attempt `simpleenroll` → rejection with recorded blocked event. 3. Attempt `simplereenroll` with the still-valid certificate → rejection. 4. Inspect the recorded event reason. |
| Expected results | All enrollment paths denied after trust removal; blocked event recorded with reason. |
| Acceptance criteria | No new credential issuance through any path after trust removal (UR-009). |
| Evidence status | Covered — AS-IS automated test: `tests/Kryptonian.MedicalDevice.Enrollment.Tests/GatewayTrustTests.cs` (CODE-OBSERVED). |

#### VT-019 — Device status lifecycle transitions

| Field | Value |
|---|---|
| Test ID | VT-019 |
| Linked requirements | SR-F-REG.004 |
| Linked THR/ORK | — |
| Level | Integration |
| Preconditions | Device records in each lifecycle state (pending, active, untrusted, retired) |
| Test inputs | State-transition operations per KSPEC-004 §8.1 state machine |
| Procedure | 1. Exercise legal transitions. 2. Attempt illegal transitions. 3. Verify inventory reflects each state. |
| Expected results | Legal transitions succeed; illegal ones rejected with distinguishable errors. |
| Acceptance criteria | State machine conformance to KSPEC-004 §8.1. |
| Evidence status | Proposed — state-machine traversal test not present as a dedicated AS-IS test; partial coverage via `DeviceServiceActivationTests.cs` (CODE-OBSERVED). |

#### VT-020 — Anonymous endpoint abuse controls (TO-BE)

| Field | Value |
|---|---|
| Test ID | VT-020 |
| Linked requirements | SR-F-REG.007 (Missing), SR-Z.008 |
| Linked THR/ORK | THR-013 |
| Level | Negative / performance — TO-BE |
| Preconditions | TO-BE abuse-control implementation (threshold TBD, SR-F-REG.007) |
| Test inputs | Sustained anonymous enrollment attempts above the configured threshold |
| Procedure | 1. Flood the anonymous endpoint. 2. Observe throttling/lockout behavior. 3. Verify legitimate traffic recovers. |
| Expected results | Abuse throttled per configured controls; documented anonymous surface unchanged (SR-Z.008). |
| Acceptance criteria | Threshold enforced; no bypass via header spoofing; event logging captures the abuse. |
| Evidence status | Proposed — SR-F-REG.007 is Missing at baseline. |


### 6.4 Certificate lifecycle tests (VT-021 – VT-030)

#### VT-021 — Approval gates enrollment

| Field | Value |
|---|---|
| Test ID | VT-021 |
| Linked requirements | SR-F-CLM.001 |
| Linked THR/ORK | THR-002; ORK-004 |
| Level | Integration |
| Preconditions | Pending device record not yet approved; active E3 backend |
| Test inputs | Enrollment attempt before approval; after approval |
| Procedure | 1. Attempt enrollment before approval → denied. 2. Approve the device. 3. Repeat enrollment → success. |
| Expected results | Denial before approval; success after; each attempt recorded as an enrollment event (SR-C.010). |
| Acceptance criteria | No credential issued for any unapproved record. |
| Evidence status | Covered — AS-IS automated test: `tests/RTSec.Kryptonian.Application.Tests/Services/EnrollmentOrchestratorTests.cs` (CODE-OBSERVED). |

#### VT-022 — Automatic renewal without re-approval

| Field | Value |
|---|---|
| Test ID | VT-022 |
| Linked requirements | SR-F-CLM.002, SR-C.004 |
| Linked THR/ORK | THR-011; ORK-001 |
| Level | Certificate renewal (live boundary) |
| Preconditions | Active device with certificate inside the renewal window; device simulator (E1) |
| Test inputs | Renewal request via `simplereenroll` within the window; renewal attempt outside the window |
| Procedure | 1. Trigger renewal in-window with the existing certificate → succeeds without administrator action. 2. Attempt out-of-window renewal → denied per profile policy. 3. Confirm no re-approval was required or recorded. |
| Expected results | In-window renewal succeeds unattended; out-of-window denied with distinguishable error. |
| Acceptance criteria | Renewal authorization derives from the existing approved identity only. |
| Evidence status | Covered — executed local evidence (VERIFIED): tracker records "Twenty device enrollment/lifecycle tests passed including actual Schannel mTLS with the public client's returned certificate"; renewal evidence "renewal uses a native TLS client certificate". AS-IS tests: `EstEnrollmentClientRenewalTests.cs` (CODE-OBSERVED). |

#### VT-023 — Renewal issues a new key and preserves the approved identity

| Field | Value |
|---|---|
| Test ID | VT-023 |
| Linked requirements | SR-F-CLM.003, SR-A.008, SR-O.003 |
| Linked THR/ORK | THR-006; ORK-001, ORK-015 |
| Level | Certificate renewal (live boundary) |
| Preconditions | Device with an installed certificate and key material (Schannel store on E1) |
| Test inputs | Renewal cycle; comparison of old and new certificates |
| Procedure | 1. Renew. 2. Compare subject/SAN identity old vs. new. 3. Confirm the new certificate carries a newly generated key (old private key not reused). 4. Confirm the installed credential is replaced durably. |
| Expected results | Identity preserved; new key generated; installed credential replaced and persisted across app restart. |
| Acceptance criteria | Key generation fresh per renewal; identity fields unchanged; Windows key persistence regression holds. |
| Evidence status | Covered — executed local evidence (VERIFIED): tracker records renewal "generates a new key and preserves approved identity" and the Schannel key-persistence fix (PR 50) with a runnable regression (`InstalledCertificateRenewalTests.cs`, CODE-OBSERVED). |

#### VT-024 — Renewal window authorization

| Field | Value |
|---|---|
| Test ID | VT-024 |
| Linked requirements | SR-C.004 |
| Linked THR/ORK | THR-011 |
| Level | Certificate renewal / unit |
| Preconditions | Profile with a defined renewal window; certificates at window edge |
| Test inputs | Re-enroll attempts at various times before expiry (inside, at boundary, outside) |
| Procedure | 1. Attempt re-enroll at each time point. 2. Compare authorization outcome with the profile's window definition. |
| Expected results | Authorized exactly within the window; outside denied. |
| Acceptance criteria | Boundary semantics deterministic; denied attempts recorded. |
| Evidence status | Covered — AS-IS automated test: `EstEnrollmentClientRenewalTests.cs` (window logic CODE-OBSERVED). |

#### VT-025 — Revocation with issuer-scoped signed CRLs and CRL distribution points

| Field | Value |
|---|---|
| Test ID | VT-025 |
| Linked requirements | SR-F-CLM.007, SR-C.005, SR-C.006 |
| Linked THR/ORK | THR-010; ORK-008 |
| Level | Revocation (live boundary) |
| Preconditions | Two issuers (profile + developer trust) with issued certificates; DeviceAdmin actor |
| Test inputs | Revocation of a certificate under each issuer; CRL fetch per issuer |
| Procedure | 1. Revoke a certificate. 2. Fetch the issuer-scoped CRL from `api/crl/{fingerprint}.crl`. 3. Verify the signature, issuer scoping, and monotonic CRL number. 4. Verify CRL distribution points present on issued certificates. 5. Confirm the other issuer's CRL is unchanged. |
| Expected results | Signed issuer-scoped CRL contains only the target serial; CRL DP points at the issuer's CRL URL. |
| Acceptance criteria | Cross-issuer isolation; signature valid against the issuing CA; transactional state (VT-026). |
| Evidence status | Covered — executed local evidence (VERIFIED): tracker records "Administrator revocation followed by native online X509Chain reporting Revoked, independently of registry lookup and without flushing the system CRL cache."; AS-IS tests: `CertificateRevocationServiceTests.cs`, `IssuerCrlStatePersistenceTests.cs`, `PkcsServiceTests.cs` (CODE-OBSERVED). |


#### VT-026 — Transactional revocation and concurrent-update conflicts

| Field | Value |
|---|---|
| Test ID | VT-026 |
| Linked requirements | SR-F-CLM.008, SR-R.004, SR-X.003 |
| Linked THR/ORK | THR-010; ORK-008 |
| Level | Revocation + concurrency |
| Preconditions | Revocable certificate; two concurrent actors (or interleaved operations) |
| Test inputs | Concurrent revocation and status-change operations; revocation against a certificate from a rotated CA |
| Procedure | 1. Issue concurrent conflicting revocation/status updates. 2. Verify the loser receives HTTP 409 and no overwrite occurs. 3. Repeat revocation after CA rotation → 409 per SR-X.003. |
| Expected results | Atomic persistence; 409 on conflicts; certificate status never un-revoked by a losing writer. |
| Acceptance criteria | No partial trust state after any conflict; issuer-mismatch guard returns 409. |
| Evidence status | Covered — AS-IS automated test: `CertificateRevocationServiceTests.cs` (transactional and conflict paths CODE-OBSERVED); `UnitOfWork.cs` conflict handling CODE-OBSERVED. |

#### VT-027 — Revoked devices denied renewal

| Field | Value |
|---|---|
| Test ID | VT-027 |
| Linked requirements | SR-F-CLM.009 |
| Linked THR/ORK | THR-010 |
| Level | Revocation / renewal (negative) |
| Preconditions | Revoked device still holding its (now revoked) certificate |
| Test inputs | `simplereenroll` with the revoked certificate |
| Procedure | 1. Attempt re-enroll with the revoked certificate. 2. Inspect the denial reason and event. |
| Expected results | Renewal denied with a revocation-specific reason. |
| Acceptance criteria | Revocation state takes precedence over renewal authorization. |
| Evidence status | Covered — AS-IS automated test: `CertificateRevocationServiceTests.cs` (CODE-OBSERVED); revocation-online enforcement at peers VERIFIED per tracker (see VT-028). |

#### VT-028 — Online revocation checking at TLS peers

| Field | Value |
|---|---|
| Test ID | VT-028 |
| Linked requirements | SR-C.007 |
| Linked THR/ORK | THR-010; ORK-008 |
| Level | DICOM TLS interoperability / revocation (live boundary) |
| Preconditions | Gateway and demonstration DICOM peers configured with issuer trust and CRL URL; CRL served over the HTTP listener |
| Test inputs | Peer handshakes before and after administrator revocation |
| Procedure | 1. Establish mTLS association → succeeds. 2. Administrator revokes the peer certificate. 3. Re-attempt the handshake with online checking enabled, without flushing the system CRL cache. |
| Expected results | Pre-revocation succeeds; post-revocation fails with a native X509Chain `Revoked` status. |
| Acceptance criteria | Revocation observed through OS chain evaluation, independent of any gateway-side registry lookup. |
| Evidence status | Covered — executed local evidence (VERIFIED): tracker records "Administrator revocation followed by native online X509Chain reporting Revoked, independently of registry lookup and without flushing the system CRL cache." and "Both peers enforce issuer, TLS purpose and online revocation." |

#### VT-029 — Expiry visibility, automated scan, notification, and watcher fault tolerance

| Field | Value |
|---|---|
| Test ID | VT-029 |
| Linked requirements | SR-F-CLM.004, SR-F-CLM.005, SR-F-CLM.006, SR-R.001, SR-R.003 |
| Linked THR/ORK | THR-011; ORK-001, ORK-007, ORK-014 |
| Level | Unit + integration (fault injection) |
| Preconditions | Certificates at varying expiry distances; SMTP settings configured (or stubbed sender) |
| Test inputs | Expiry scan cycles; a scan that throws mid-cycle; certificates approaching/missing renewal |
| Procedure | 1. Run `CertificateExpiryWatcher` scan → notifications queued for at-risk certificates. 2. Inject an exception in `ScanOnceAsync` → watcher logs and continues the loop. 3. Verify `LastExpiryNotifiedAt` prevents duplicate alerts and notification failure does not corrupt lifecycle state. |
| Expected results | Alerts for approaching/non-renewed certificates; watcher survives faults; state unchanged on notification failure. |
| Acceptance criteria | Non-renewal alert generated per UR-012; no crash-loop; no state corruption. |
| Evidence status | Partially covered — AS-IS automated test: `NotificationDispatcherTests.cs` (CODE-OBSERVED); watcher fault-tolerance and end-to-end email delivery Proposed (tracker records delivery behavior at baseline as unexercised). |

#### VT-030 — Profile policy bundle enforcement (unit)

| Field | Value |
|---|---|
| Test ID | VT-030 |
| Linked requirements | SR-C.001, SR-C.003 |
| Linked THR/ORK | THR-004, THR-005; ORK-004 |
| Level | Unit |
| Preconditions | Profile fixtures with differing subject-DN rules, key usage, EKU, validity |
| Test inputs | CSRs and CA responses matching and violating each policy element |
| Procedure | 1. Run CSR/returned-certificate policy evaluation per fixture. 2. Assert accept/reject per element. 3. Verify partial profile updates preserve omitted trust lists (PR 47). |
| Expected results | Every policy element independently enforced on both CSR acceptance and returned-certificate validation. |
| Acceptance criteria | No mis-issued credential accepted by policy validation; partial update preserves trust lists. |
| Evidence status | Covered — AS-IS automated test: `tests/RTSec.Kryptonian.Application.Tests/Services/EnrollmentCertificatePolicyTests.cs` and `EstProfilePartialUpdateTests.cs` (CODE-OBSERVED). |


### 6.5 Security, authentication, and authorization tests (VT-031 – VT-039)

#### VT-031 — Transport security for credential-bearing exchanges (incl. CORS)

| Field | Value |
|---|---|
| Test ID | VT-031 |
| Linked requirements | SR-S.001, SR-S.013 |
| Linked THR/ORK | THR-003, THR-016 |
| Level | Integration (security) |
| Preconditions | Gateway with default HTTPS redirection and CORS policy `KryptonianUi` |
| Test inputs | Credential-bearing requests over plain HTTP; CORS preflight from a disallowed origin; from an allowed origin |
| Procedure | 1. Attempt credential submission over plain HTTP → refused (VT-010). 2. CORS preflight from a disallowed origin → no `KryptonianUi` grant. 3. Allowed origin → permitted. |
| Expected results | No credential accepted unencrypted; CORS restricted to configured origins. |
| Acceptance criteria | Transport security holds for EST, admin API, and notification-credential surfaces. |
| Evidence status | Partially covered — HTTPS-only EST VERIFIED per tracker (see VT-010); CORS assertions Proposed (no dedicated AS-IS test observed). |

#### VT-032 — CSR and returned-certificate policy enforcement (live boundary)

| Field | Value |
|---|---|
| Test ID | VT-032 |
| Linked requirements | SR-S.002 |
| Linked THR/ORK | THR-004, THR-005 |
| Level | Security (live boundary) |
| Preconditions | Approved device; E3 backend; profile policy bundle active |
| Test inputs | CSR spoofing subject-DN / key usage / EKU / validity; a compromised backend response with a non-conforming certificate |
| Procedure | 1. Submit policy-violating CSRs → rejected before CA contact. 2. Force a backend response violating the profile → rejected before delivery to the device. |
| Expected results | Both directions enforced: inbound CSR and outbound issued certificate. |
| Acceptance criteria | No policy-violating credential ever delivered to a device (PR 43 behavior). |
| Evidence status | Covered — AS-IS automated test: `EnrollmentCertificatePolicyTests.cs` (CODE-OBSERVED); PR 43 ("CSR and returned-certificate policy enforcement") per tracker. |

#### VT-033 — Gateway-owned trust-policy gate and durability

| Field | Value |
|---|---|
| Test ID | VT-033 |
| Linked requirements | SR-S.003, SR-C.002 |
| Linked THR/ORK | THR-012; ORK-005 |
| Level | Integration (security) |
| Preconditions | Profile with persisted trust policy; DeviceAdmin actor |
| Test inputs | Enrollment from an untrusted device; trust-policy updates (additive); gateway restart |
| Procedure | 1. Attempt enrollment from a device outside the trust policy → rejected at the gateway, before CA contact. 2. Apply an additive trust update (PR 41/47). 3. Restart the gateway on populated SQLite and re-check the policy. |
| Expected results | Gateway enforces its own trust gate; trust policy survives restart unchanged. |
| Acceptance criteria | Trust decision never delegated to the CA backend; durable trust per SR-C.002. |
| Evidence status | Covered — executed local evidence (VERIFIED): tracker records "Gateway restart on populated SQLite preserved configured profile, issuer and devices."; AS-IS tests: `EstProfilePersistenceTests.cs`, `EstProfilePartialUpdateTests.cs` (CODE-OBSERVED). |

#### VT-034 — Forwarded-certificate guard and authenticated forwarder identity

| Field | Value |
|---|---|
| Test ID | VT-034 |
| Linked requirements | SR-S.004, SR-S.005 (Missing) |
| Linked THR/ORK | THR-017 |
| Level | Security (negative) |
| Preconditions | Gateway with the XFCC forwarding middleware and known-proxy configuration |
| Test inputs | Directly forwarded `X-Forwarded-Client-Cert` from a non-proxy source; duplicate/dual-identity headers; untrusted `X-Device-Id`; spoofed `X-Forwarded-For` |
| Procedure | 1. Send a spoofed forwarded certificate directly to the EST endpoint → rejected. 2. Send duplicate/dual-identity combinations → rejected. 3. Analyze (TO-BE) the authenticated-forwarder design gap of SR-S.005. |
| Expected results | Spoofed forwarded identities rejected; only trusted-proxy-forwarded identities honored. |
| Acceptance criteria | No spoofed identity reaches enrollment logic; proxy-trust boundary limitation recorded (source-IP trust, SR-S.005). |
| Evidence status | Partially covered — executed local evidence (VERIFIED): tracker records "Direct forwarded-certificate spoof rejected"; AS-IS test: `tests/RTSec.Kryptonian.Api.Tests/Middleware/CertificateForwardingGuardTests.cs` (CODE-OBSERVED); SR-S.005 (authenticated forwarder) Missing → that portion Proposed. |

#### VT-035 — JWT signing secret provisioning (TO-BE)

| Field | Value |
|---|---|
| Test ID | VT-035 |
| Linked requirements | SR-S.007 (Missing), SR-A.006 (Missing) |
| Linked THR/ORK | THR-007, THR-009, THR-016 |
| Level | Security (negative) — TO-BE |
| Preconditions | TO-BE fail-closed secret provisioning implemented |
| Test inputs | Startup without a provisioned JWT secret (production mode); with a below-threshold secret; with a provisioned secret |
| Procedure | 1. Start without secret in Production → startup fails closed. 2. Start with weak secret → rejected (≥32-character rule pending confirmation, SR-A.006). 3. Start with valid secret → normal operation. |
| Expected results | No hard-coded fallback ever used outside explicit development confinement (SR-A.009). |
| Acceptance criteria | Production boot refuses weak/absent signing secrets. |
| Evidence status | Proposed — SR-S.007/SR-A.006 Missing at baseline (hard-coded dev fallback recorded as a weak control in KSPEC-003 §22). |


#### VT-036 — Password hashing strength

| Field | Value |
|---|---|
| Test ID | VT-036 |
| Linked requirements | SR-S.009 |
| Linked THR/ORK | THR-009 |
| Level | Inspection |
| Preconditions | Source access to user-credential handling |
| Test inputs | Inspection checklist: hash algorithm, work factor, salt handling |
| Procedure | 1. Inspect password storage and verification code paths. 2. Confirm BCrypt with work factor ≥ 12. 3. Confirm no plaintext or reversible storage. |
| Expected results | BCrypt ≥ 12 everywhere passwords are stored. |
| Acceptance criteria | Inspection record completed with file/line citations. |
| Evidence status | Proposed — Inspection-method requirement; no automated test applicable. (Code path CODE-OBSERVED in `UsersController.cs`/auth services per KSPEC-003 evidence.) |

#### VT-037 — Administrator authentication schemes and token validation

| Field | Value |
|---|---|
| Test ID | VT-037 |
| Linked requirements | SR-A.001, SR-A.002, SR-A.010, SR-F-ADM.003 |
| Linked THR/ORK | THR-007, THR-009 |
| Level | Authentication |
| Preconditions | Seeded SystemAdmin (SR-F-ADM.005 bootstrap); API key issued |
| Test inputs | Login with valid/invalid credentials; JWT with valid/expired/tampered signature; deactivated user's token; login attempts for existent/non-existent users |
| Procedure | 1. Authenticate via password → JWT issued. 2. Authenticate via API key → honored. 3. Replay expired/tampered tokens → 401. 4. Use a deactivated user's token → 401 (lockout). 5. Compare error responses for existent vs. non-existent users (non-enumeration). |
| Expected results | Only valid fresh credentials authenticate; deactivated users locked out; no user enumeration. |
| Acceptance criteria | Dual-scheme behavior per SR-A.001; validation failures distinguishable from authorization failures. |
| Evidence status | Partially covered — admin-setup behavior is documented; device lifecycle tests do not establish user-management or JWT coverage (DOCUMENTED only); dedicated token-validation tests Proposed (no dedicated AS-IS test file observed under `tests/RTSec.Kryptonian.Api.Tests/Authentication/`). |

#### VT-038 — Role-based authorization enforcement

| Field | Value |
|---|---|
| Test ID | VT-038 |
| Linked requirements | SR-Z.001, SR-Z.002, SR-Z.003, SR-Z.004, SR-Z.005, SR-Z.010 |
| Linked THR/ORK | THR-007 |
| Level | Authorization |
| Preconditions | One actor per role (Standard, DeviceAdmin, SystemAdmin) |
| Test inputs | Full endpoint matrix: device mutations, system configuration, revocation, read-only queries, per role |
| Procedure | 1. Execute each endpoint × role combination. 2. Record allow/deny. 3. Verify no role gains clinical-capability statements from device state (SR-Z.010). |
| Expected results | Device ops restricted to DeviceAdmin+; system config to SystemAdmin; read-only for Standard; revocation DeviceAdmin+. |
| Acceptance criteria | Zero unauthorized successes in the matrix. |
| Evidence status | Covered — AS-IS automated tests: `tests/RTSec.Kryptonian.Api.Tests/Controllers/DevicesControllerAuthorizationTests.cs`, `CertificateRevocationControllerAuthorizationTests.cs` (CODE-OBSERVED). |

#### VT-039 — API-key authentication and claims binding

| Field | Value |
|---|---|
| Test ID | VT-039 |
| Linked requirements | SR-A.003, SR-A.004, SR-A.005, SR-Z.006, SR-F-ADM.004, UR-027 |
| Linked THR/ORK | THR-007 |
| Level | Authentication / authorization |
| Preconditions | Users of differing roles; API-key management endpoint available |
| Test inputs | Created/revoked API keys; key of a deactivated user; key used after role change |
| Procedure | 1. Create a key for a role → authenticate → role claims honored. 2. Revoke the key → 401. 3. Deactivate the owning user → key stops working. 4. Change the user's role → existing key retains generation-time role (SR-A.005) and claims come only from the stored record (SR-Z.006). |
| Expected results | Keys hashed at rest; lifecycle-bound to owner; claims never spoofable via headers. |
| Acceptance criteria | All key lifecycle transitions enforced; static compatibility keys limited per SR-A.004. |
| Evidence status | Proposed — behavior CODE-OBSERVED in `ApiKeyAuthenticationHandler.cs` per KSPEC-003 evidence, but no dedicated AS-IS test file observed; assertion set Proposed. |

### 6.6 CA interoperability tests (VT-040 – VT-043)

#### VT-040 — Multi-backend configuration, test-before-activate, profile-bound routing

| Field | Value |
|---|---|
| Test ID | VT-040 |
| Linked requirements | SR-F-CAI.001, SR-F-CAI.002, SR-F-CAI.004 |
| Linked THR/ORK | ORK-003, ORK-010 |
| Level | Integration + Analysis |
| Preconditions | Two configured backends (E3 self-signed + harness) |
| Test inputs | Backend test invocation; activation of exactly one backend; device enrollment without backend identifier |
| Procedure | 1. Configure backends via admin API/portal. 2. Run the backend test → result reported before activation. 3. Activate one backend; enroll a device without sending a backend identifier. 4. Verify transparent protocol mapping (SR-F-CAI.004, Analysis). |
| Expected results | Test results reported pre-activation; enrollments and cacerts route to their profile-bound enabled backend, even when a different backend is globally active, with no device-side backend knowledge. |
| Acceptance criteria | Exactly one active backend effective; device requests carry no backend identity. |
| Evidence status | Covered — AS-IS automated test: `tests/RTSec.Kryptonian.Application.Tests/Services/CaBackendServiceTests.cs` (CODE-OBSERVED). |


#### VT-041 — Self-signed CA: issuance, CRL generation, material persistence, fail-closed unsupported revocation

| Field | Value |
|---|---|
| Test ID | VT-041 |
| Linked requirements | SR-F-CAI.008, SR-F-CAI.009, SR-X.001 |
| Linked THR/ORK | THR-008; ORK-008 |
| Level | Unit + revocation |
| Preconditions | Self-signed backend configured; certificates issued by it |
| Test inputs | Issuance requests; CRL generation; revocation request against a backend without CRL capability |
| Procedure | 1. Issue a certificate via the self-signed connector. 2. Generate and verify its CRL. 3. Persist CA material and reload. 4. Call revocation on a backend without CRL generation → expect explicit not-implemented error and unchanged certificate status. |
| Expected results | Issuance/CRL/persistence work; unsupported revocation fails closed (no false success). |
| Acceptance criteria | Certificate status unchanged after failed revocation; error distinguishable (HTTP 501 per SR-X.001). |
| Evidence status | Covered — AS-IS automated tests: `tests/RTSec.Kryptonian.Infrastructure.Tests/Crypto/SelfSignedCaConnectorTests.cs`, `CertificateRevocationServiceTests.cs` (fail-closed path CODE-OBSERVED); live revocation per tracker (VT-025). |

#### VT-042 — ACME enrollment and functional challenge set

| Field | Value |
|---|---|
| Test ID | VT-042 |
| Linked requirements | SR-F-CAI.007 |
| Linked THR/ORK | THR-005 |
| Level | Integration |
| Preconditions | ACME backend configuration against a loopback ACME endpoint (E3); external account binding configured |
| Test inputs | http-01 challenge flow; dns-01 (stub at baseline); an unimplemented challenge type in configuration |
| Procedure | 1. Complete an http-01 enrollment via the challenge store/`WellKnownServers`. 2. Attempt dns-01 → observe stub behavior (KSPEC-004 Dns01ChallengeStub). 3. Configure an unimplemented challenge type → rejected at configuration time. |
| Expected results | http-01 functional; unimplemented challenge types rejected at configuration rather than failing at issuance. |
| Acceptance criteria | No silent issuance failure for unsupported challenges; external account binding exercised. |
| Evidence status | Covered — AS-IS automated tests: `tests/RTSec.Kryptonian.Infrastructure.Tests/Acme/AcmeCaConnectorTests.cs`, `Http01ChallengeStoreTests.cs`, `AcmeConnectorConfigTests.cs`, `WellKnownServersTests.cs` (CODE-OBSERVED); dns-01 remains Partial (SR-F-CAI.007). |

#### VT-043 — ADCS/EJBCA hospital interoperability (unverified at baseline)

| Field | Value |
|---|---|
| Test ID | VT-043 |
| Linked requirements | SR-F-CAI.005, SR-F-CAI.006, SR-F-CAI.003 |
| Linked THR/ORK | THR-005, THR-008; ORK-003, ORK-008 |
| Level | CA interoperability (external lab) |
| Preconditions | ADCS lab per docs/adcs-real-backend-demo.md (Windows Server + NDES) or EJBCA instance; configurable SCEP endpoints |
| Test inputs | Real enrollment via SCEP/NDES; real revocation via EJBCA REST; misconfigured issuer-DN revocation |
| Procedure | 1. Enroll through real ADCS/NDES with configurable endpoints. 2. Revoke through real EJBCA; verify failure reported honestly when the issuer DN is unconfigured. 3. Compare against docs/adcs-real-backend-demo.md connector gaps 1–5. |
| Expected results | Enrollment/revocation interoperable with real hospital CA classes, or the gap documented honestly. |
| Acceptance criteria | No false success; configurable (not hard-coded) endpoints. |
| Evidence status | Proposed — tracker records "actual ADCS/EJBCA hospital enrollment/revocation interoperability remains unverified"; connector code CODE-OBSERVED (`AdcsCaConnector.cs`, `EjbcaCaConnector.cs`, `ScepClient.cs`). |

### 6.7 Negative, fault-injection, and concurrency tests (VT-044 – VT-048)

#### VT-044 — EST negative request matrix

| Field | Value |
|---|---|
| Test ID | VT-044 |
| Linked requirements | SR-F-EST.003, SR-F-EST.004, SR-F-EST.013, SR-P.002 |
| Linked THR/ORK | THR-004, THR-013 |
| Level | Negative |
| Preconditions | Gateway running; valid baseline request captured |
| Test inputs | Malformed CSR; wrong content type; unknown HTTP method; oversized body; missing auth; duplicated headers |
| Procedure | 1. Send each mutation of the baseline request. 2. Record (status, error, events). 3. Confirm no state change for any rejection. |
| Expected results | Every mutation rejected with a distinguishable error; no partial state. |
| Acceptance criteria | Zero enrollments created by negative inputs; error classes match ICD-ERR.001. |
| Evidence status | Partially covered — `EstControllerTests.cs`/`EstRouteTests.cs` cover core rejections (CODE-OBSERVED); the full mutation matrix Proposed. |

#### VT-045 — CA backend failure outcome recording

| Field | Value |
|---|---|
| Test ID | VT-045 |
| Linked requirements | SR-R.002, SR-F-CAI.004 |
| Linked THR/ORK | ORK-003 |
| Level | Fault injection |
| Preconditions | Active backend replaced by an unreachable/failing stub |
| Test inputs | Enrollment while backend is down / returns errors |
| Procedure | 1. Make the active backend unavailable. 2. Enroll → observe device-visible error. 3. Inspect enrollment events for the recorded failure outcome. 4. Restore backend → next enrollment succeeds. |
| Expected results | Distinguishable "backend down" outcome to device and events; no partial trust state; recovery without operator cleanup. |
| Acceptance criteria | Error class differs from authorization rejections; no orphaned state. |
| Evidence status | Covered — AS-IS automated test: `EnrollmentOrchestratorTests.cs` (error paths CODE-OBSERVED). |


#### VT-046 — Invalid replacement credentials fail closed; proxy client rejection

| Field | Value |
|---|---|
| Test ID | VT-046 |
| Linked requirements | SR-X.002 |
| Linked THR/ORK | THR-008, THR-012; ORK-015 |
| Level | DICOM TLS interoperability / fault injection (live boundary) |
| Preconditions | DIMSE-TLS proxy running with a valid PFX; loopback clients (E1) |
| Test inputs | Client certificates unknown to the issuer / expired / revoked; externally replaced PFX that is expired |
| Procedure | 1. Connect clients with unknown, expired, and revoked certificates → each rejected. 2. Replace the proxy PFX with an expired credential → new handshakes must fail closed; no fallback to the stale credential. |
| Expected results | All invalid credentials rejected; invalid replacement never kept as fallback. |
| Acceptance criteria | Fail-closed on every invalid credential class. |
| Evidence status | Covered — executed local evidence (VERIFIED): tracker records "Five loopback proxy tests passed, including unknown/expired/revoked client rejection and fresh handshakes observing atomic PFX replacement; expired replacement fails closed."; AS-IS tests: `DimseTlsProxyWorkerTests.cs`, `ServerCertificateRotationTests.cs` (CODE-OBSERVED). |

#### VT-047 — DICOM mTLS C-STORE with SOP Instance UID comparison

| Field | Value |
|---|---|
| Test ID | VT-047 |
| Linked requirements | SR-F-INT.005, SR-C.007 |
| Linked THR/ORK | THR-001; ORK-002 |
| Level | DICOM TLS interoperability (live boundary) |
| Preconditions | Both DICOM peers with issued certificates, issuer trust, TLS purpose, online revocation enabled |
| Test inputs | Two DIMSE associations with C-STORE transfers of DICOM objects |
| Procedure | 1. Perform two actual mTLS C-STORE transfers. 2. Reopen the received files. 3. Compare received SOP Instance UIDs against the sent objects. 4. Record the UIDs and issuer fingerprint in evidence.json. |
| Expected results | Both transfers complete; received-file SOP UIDs match sent UIDs exactly. |
| Acceptance criteria | Both peers enforce issuer, TLS purpose, and online revocation; UID comparison non-empty and exact. |
| Evidence status | Covered — executed local evidence (VERIFIED): tracker records "Two actual mutual-TLS DICOM C-STORE transfers followed by received-file reopen and SOP Instance UID comparison. Both peers enforce issuer, TLS purpose and online revocation." with SOP UIDs `2.25.154518358314734480382348393848679450333` and `2.25.281306672341327719638925984637794989584`; parser unit test `tests/RTSec.Kryptonian.Bridge.Tests/DicomWebMultipartParserTests.cs`. |

#### VT-048 — Concurrent activation code consumption race

| Field | Value |
|---|---|
| Test ID | VT-048 |
| Linked requirements | SR-F-REG.002, SR-R.004 |
| Linked THR/ORK | THR-003, THR-006; ORK-005 |
| Level | Concurrency |
| Preconditions | Single valid activation code |
| Test inputs | Parallel enrollment attempts using the same code |
| Procedure | 1. Launch N concurrent enrollments with the same code. 2. Count successes. 3. Verify exactly one success and N−1 distinguishable failures; check persisted state. |
| Expected results | Exactly one consumption; no duplicate credential issuance for one code. |
| Acceptance criteria | Atomic one-time consumption under race; losers rejected cleanly. |
| Evidence status | Covered — AS-IS automated test: `tests/RTSec.Kryptonian.Infrastructure.Tests/Data/DeviceActivationConcurrencyTests.cs` (CODE-OBSERVED); replay rejection VERIFIED per tracker (VT-015). |

### 6.8 Data, persistence, and recovery tests (VT-049 – VT-054)

#### VT-049 — Gateway restart preserves state (SQLite)

| Field | Value |
|---|---|
| Test ID | VT-049 |
| Linked requirements | SR-D.001, SR-V.003 |
| Linked THR/ORK | ORK-006, ORK-012 |
| Level | Database persistence |
| Preconditions | Gateway on E1 with populated SQLite: profile, issuer state, devices, enrollment events |
| Test inputs | Graceful stop; restart; post-restart API queries |
| Procedure | 1. Populate all state types. 2. Stop the gateway. 3. Restart. 4. Query profile, issuer, devices, events. 5. Enroll again to confirm operational continuity. |
| Expected results | Configured profile, issuer, and devices preserved exactly; new enrollments work. |
| Acceptance criteria | No configuration loss across restart (UR-037). |
| Evidence status | Covered — executed local evidence (VERIFIED): tracker records "Gateway restart on populated SQLite preserved configured profile, issuer and devices." |

#### VT-050 — UTC timestamp discipline

| Field | Value |
|---|---|
| Test ID | VT-050 |
| Linked requirements | SR-D.005 |
| Linked THR/ORK | ORK-009 |
| Level | Database persistence (regression) |
| Preconditions | SQLite persistence; CRL refresh path exercised on a host with non-UTC local time |
| Test inputs | CRL refresh with `DateTimeKind` unspecified vs. UTC |
| Procedure | 1. Refresh an issuer CRL. 2. Compare stored revocation times with UTC wall-clock. 3. Run the baseline regression covering the SQLite DateTime-kind defect. |
| Expected results | All persisted timestamps UTC-stable; no timezone drift. |
| Acceptance criteria | Regression test reproduces the historical five-hour shift when the fix is absent and passes with it. |
| Evidence status | Covered — executed local evidence (VERIFIED): tracker records "SQLite's unspecified DateTime kind shifted refreshed CRL revocation times five hours into the future on this host ... fixed with runnable regressions." |


#### VT-051 — Additive schema upgrades

| Field | Value |
|---|---|
| Test ID | VT-051 |
| Linked requirements | SR-D.003 |
| Linked THR/ORK | ORK-011 |
| Level | Database persistence (unit) |
| Preconditions | Database at the prior schema version containing data |
| Test inputs | Schema-upgrade runner over a populated database |
| Procedure | 1. Create a database at the previous schema with representative rows. 2. Run the upgrade. 3. Verify data preserved and new schema objects present. 4. Run the application against the upgraded database. |
| Expected results | Upgrades are additive; existing rows survive. |
| Acceptance criteria | No destructive migration without the TO-BE upgrade safety boundary (SR-D.004, Missing). |
| Evidence status | Covered — AS-IS automated test: `tests/RTSec.Kryptonian.Infrastructure.Tests/Data/SchemaUpgradesTests.cs` (CODE-OBSERVED). |

#### VT-052 — PostgreSQL provider parity

| Field | Value |
|---|---|
| Test ID | VT-052 |
| Linked requirements | SR-D.002, UR-037 |
| Linked THR/ORK | ORK-006, ORK-012 |
| Level | Database persistence |
| Preconditions | E2 with PostgreSQL (Npgsql provider) |
| Test inputs | Full lifecycle scenario suite against the PostgreSQL-backed gateway |
| Procedure | 1. Deploy with PostgreSQL via docker-compose. 2. Execute the enrollment → renewal → revocation → restart scenario. 3. Compare outcomes with the SQLite baseline (VT-049). |
| Expected results | Identical behavior across providers. |
| Acceptance criteria | Parity on all state transitions; no provider-specific timestamp or casing defects. |
| Evidence status | Proposed — tracker records "PostgreSQL schema upgrades are implemented but only SQLite persistence was exercised." |

#### VT-053 — Backup and restore procedure

| Field | Value |
|---|---|
| Test ID | VT-053 |
| Linked requirements | SR-X.007 (Partial), SR-D.006 |
| Linked THR/ORK | ORK-006, ORK-012 |
| Level | Recovery (Demonstration) |
| Preconditions | Populated gateway; backup artifacts produced per the documented procedure |
| Test inputs | Backup file; corrupted backup (negative) |
| Procedure | 1. Back up the populated store. 2. Degrade/destroy the live store. 3. Restore from backup. 4. Verify device/profile/issuer state and CRL continuity. 5. Attempt restore from a corrupted backup → clean failure. |
| Expected results | Restored gateway resumes with preserved trust state; corrupted backups fail loudly. |
| Acceptance criteria | Restore completes without orphaned CRL state; procedure documented (SR-X.007 Partial — retention policy TBD per SR-D.006). |
| Evidence status | Proposed — no restore drill recorded at baseline. |

#### VT-054 — Graceful shutdown and cancellation

| Field | Value |
|---|---|
| Test ID | VT-054 |
| Linked requirements | SR-R.005 |
| Linked THR/ORK | — |
| Level | Recovery / inspection |
| Preconditions | Gateway running with `CertificateExpiryWatcher` active and an in-flight enrollment |
| Test inputs | Cancellation/stop signal during a scan and during an enrollment |
| Procedure | 1. Trigger stop mid-scan → observe clean cancellation (no crash). 2. Trigger stop mid-enrollment → verify no partial trust state persisted. 3. Restart and validate state. |
| Expected results | Background scans and in-flight operations stop cleanly; persisted state remains consistent. |
| Acceptance criteria | No corrupted records after any stop point. |
| Evidence status | Partially covered — cancellation handling CODE-OBSERVED in `CertificateExpiryWatcher.cs` (Inspection-level, SR-R.005 method I); mid-enrollment stop test Proposed. |

### 6.9 Performance tests (VT-055)

#### VT-055 — EST rate limiting and enrollment capacity

| Field | Value |
|---|---|
| Test ID | VT-055 |
| Linked requirements | SR-S.006, SR-P.001, UR-014 |
| Linked THR/ORK | THR-013 |
| Level | Performance |
| Preconditions | AspNetCoreRateLimit configuration active on EST paths (E1) |
| Test inputs | Sustained enrollment flood above the configured limit; legitimate traffic interleaved |
| Procedure | 1. Drive requests above the limit from one source IP. 2. Observe throttling responses (429) and logged outcomes. 3. Confirm legitimate concurrent clients complete within the absence of an approved latency budget (SR-P.003 TBD — record observed latencies only, no pass/fail). |
| Expected results | Excess traffic throttled; legitimate traffic unaffected. |
| Acceptance criteria | Throttle engages per configuration; observed behavior recorded as capacity analysis input (SR-P.001). |
| Evidence status | Proposed — tracker/UR-014 note rate limiting as configuration-documented with effectiveness not separately evidenced; production limits TBD (SR-S.006). |


### 6.10 Deployment acceptance tests (VT-056 – VT-058)

#### VT-056 — Container deployment acceptance and health/status endpoints

| Field | Value |
|---|---|
| Test ID | VT-056 |
| Linked requirements | SR-B.001, SR-B.003, SR-V.001, SR-V.002 (Missing), SR-F-ADM.010, UR-028, UR-029 |
| Linked THR/ORK | ORK-010, ORK-013 |
| Level | Deployment acceptance |
| Preconditions | E2 docker-compose stack built from the baseline commit |
| Test inputs | Compose up; health query `GET /api/status/health`; status/observe surface; external TLS certificate provisioned per SR-B.003 |
| Procedure | 1. Start the stack. 2. Verify health endpoint availability. 3. Exercise one full enrollment via EST. 4. Verify the dashboard reflects the enrollment. 5. Component-level health reporting checked against SR-V.002 (Missing — expected partial). |
| Expected results | Stack comes up; health and status endpoints respond; end-to-end enrollment succeeds in the container. |
| Acceptance criteria | Deployment acceptance = stack healthy + one successful enrollment; missing component-health detail recorded, not claimed. |
| Evidence status | Proposed — no container deployment run recorded in the tracker (Windows/SQLite developer path is the recorded evidence). |

#### VT-057 — Windows service and sandbox modes

| Field | Value |
|---|---|
| Test ID | VT-057 |
| Linked requirements | SR-B.002, SR-B.004, SR-B.007 (Partial) |
| Linked THR/ORK | ORK-010, ORK-015 |
| Level | Deployment acceptance |
| Preconditions | Windows host; sandbox bootstrap configuration (Production environment, in-memory ports/SQLite/secrets via `SandboxBootstrap`) |
| Test inputs | Sandbox-mode launch; Windows-service hosting; documented configuration settings inventory |
| Procedure | 1. Launch in sandbox mode → distinct localhost listeners for admin/EST/CRL; admin listener without client certificates. 2. Install/run as Windows service. 3. Verify documented settings produce the documented behavior (SR-B.004). |
| Expected results | Both modes operational per KSPEC-004 deployment topology. |
| Acceptance criteria | Sandbox rejects misconfiguration; service stop/restart preserves state (VT-049). |
| Evidence status | Covered — AS-IS automated test: `tests/RTSec.Kryptonian.Api.Tests/SandboxBootstrapTests.cs` (CODE-OBSERVED); sandbox mode exercised by the developer verifier evidence path per tracker. |

#### VT-058 — CI pipeline regression and dependency-advisory gate

| Field | Value |
|---|---|
| Test ID | VT-058 |
| Linked requirements | SR-O.005, SR-S.014, SR-F-INT.008 (Proposed joint vectors excluded) |
| Linked THR/ORK | THR-014; ORK-016 |
| Level | Deployment acceptance (CI) |
| Preconditions | GitHub Actions workflows present (`.github/workflows/`) |
| Test inputs | PR merge; UI `npm audit` result; solution test run |
| Procedure | 1. Confirm CI builds the UI, tests the solution, and builds the standalone proxy. 2. Confirm the UI audit gate rejects high/critical advisories. 3. Record remaining deployment-blocker advisories (SQLitePCLRaw.lib.e_sqlite3 2.1.6; System.Security.Cryptography.Xml 8.0.2). |
| Expected results | Pipeline green on baseline; advisory gate enforced; blockers disclosed rather than hidden. |
| Acceptance criteria | CI evidence exists for the baseline; blocker list matches the tracker. |
| Evidence status | Covered — executed local evidence (VERIFIED): tracker records "Windows GitHub CI builds the UI, tests the solution and builds the standalone proxy." and the npm-audit reduction "from six vulnerable packages to zero; CI now rejects high/critical UI dependency advisories", with the two remaining high advisories listed as deployment blockers. Eighteen pre-existing Windows infrastructure skips acknowledged. |


---

#### VT-059 — Complete durable lifecycle decision audit

| Field | Value |
|---|---|
| Test ID | VT-059 |
| Linked requirements | SR-C.010, SR-U.001, SR-U.002 |
| Linked THR/ORK | THR-015; ORK-008 |
| Level | API integration and persistence |
| Preconditions | Persistent test database; known profile, device, issuer; ability to query audit records independently |
| Test inputs | Unknown/disabled profile; missing/malformed bootstrap; invalid CSR/signature; identity/policy rejection; CA failure/pending; successful enroll/renew; successful/unsupported/conflicting revocation |
| Procedure | Execute each case through the API boundary; record correlation ID and response; independently query durable decision records; restart and query again. Verify no activation code, private key, token or password is recorded. |
| Expected results | One durable decision record per attempt with operation, available requester/device/profile context, trusted source attribution, outcome/reason and correlation ID; success serial where applicable. |
| Acceptance criteria | No attempt disappears because it fails before issuance or because it is a revocation. Records survive restart and exclude raw secrets. |
| Evidence status | Proposed — this review observed missing branches; this acceptance test has not been implemented or executed. |

#### VT-060 — Consumed activation failure, pending and restart recovery

| Field | Value |
|---|---|
| Test ID | VT-060 |
| Linked requirements | SR-F-REG.002, SR-R.004 |
| Linked THR/ORK | THR-003; ORK-005 |
| Level | Persistent API integration and fault injection |
| Preconditions | Pending registration and fresh code; controllable CA stub; durable database; administrator access |
| Test inputs | Backend unavailable; policy rejection after bootstrap validation; CA pending response; process termination after activation save; concurrent bootstrap reuse |
| Procedure | Inject each failure after activation consumption. Restart where applicable. Retry the same credential, inspect device/code/certificate state, regenerate a code administratively, and retry to completion. For a pending result also follow Retry-After with the public client. |
| Expected results | AS-IS characterization: consumed code cannot be replayed; device can remain Pending without a certificate. TO-BE: documented recoverable outcome, no duplicate credential issuance, and authorized regeneration succeeds. Pending retry behavior is explicitly characterized rather than presumed safe. |
| Acceptance criteria | No code reuse under concurrency/restart; no false Active state; a deterministic supported recovery path exists and the pending/bootstrap limitations are disclosed to integrators. |
| Evidence status | Proposed — consumption order is CODE-OBSERVED; failure/crash/pending recovery execution is absent. |


## 7. AS-IS coverage snapshot

This snapshot states what already exists at the baseline commit versus what is proposed.
It contains **no** new execution claims: the executed results quoted below are exactly
those recorded in `docs/IMPLEMENTATION-TRACKER.md` (local developer run, 2026-09-08, run
`verify-fa867498c35c4ca7bf58c0d4bc3acd21`), and AS-IS tests are those found in `tests/`
(8 test projects) executed by CI per the tracker.

### 7.1 VT evidence distribution (60 VTs)

| Evidence class | Count | VTs |
|---|---|---|
| Covered — executed local evidence (VERIFIED) | 15 | VT-006, VT-007, VT-010, VT-015, VT-016, VT-022, VT-023, VT-025, VT-028, VT-033, VT-046, VT-047, VT-049, VT-050, VT-058 |
| Covered — AS-IS automated test (CODE-OBSERVED; CI per tracker) | 24 | VT-001, VT-002, VT-003, VT-004, VT-005, VT-008, VT-009, VT-014, VT-017, VT-018, VT-021, VT-024, VT-026, VT-027, VT-030, VT-032, VT-038, VT-040, VT-041, VT-042, VT-045, VT-048, VT-051, VT-057 |
| Partially covered | 7 | VT-011, VT-029, VT-031, VT-034, VT-037, VT-044, VT-054 |
| Proposed | 14 | VT-012, VT-013, VT-019, VT-020, VT-035, VT-036, VT-039, VT-043, VT-052, VT-053, VT-055, VT-056, VT-059, VT-060 |
| **Total** | **60** | |

### 7.2 Requirement coverage

114 of 154 SRs have a specified VT link; 40 have no direct VT link. KSPEC-009 gives the exact list. All 38 URs now have an SR edge. These are traceability counts, not a complete executed coverage claim. Inspection and analysis methods remain obligations requiring recorded evidence. New VT-059 and VT-060 are Proposed and unexecuted. Fresh regression execution is recorded separately in KSPEC-016 and validation/verification.json.

### 7.3 Known gaps visible in the snapshot

- Remote ADCS/EJBCA enrollment/revocation interoperability (VT-043) — unverified at
  baseline; also a KSPEC-010 gap-analysis candidate.
- PostgreSQL parity (VT-052), container deployment acceptance (VT-056), backup/restore
  drill (VT-053) — not exercised at baseline.
- TO-BE security hardening tests (VT-020, VT-035, VT-012/013) gated on Missing
  requirements (SR-F-REG.007, SR-S.007/SR-A.006, SR-F-EST.011/014).
- Eighteen pre-existing Windows infrastructure skips; remaining dependency advisories
  (SQLitePCLRaw.lib.e_sqlite3 2.1.6; System.Security.Cryptography.Xml 8.0.2) are
  deployment blockers, not a clean-security-audit claim.

---

## 8. Traceability note — KSPEC-009 consumption

KSPEC-009 (`09-TRACEABILITY.md`) is the single downstream consumer of this document's
VT → SR links. Rules:

1. KSPEC-009 references VT IDs **only as defined here** (KSPEC-000 §7: IDs are stable
   and never reused); it shall not redefine test content.
2. Every VT ↔ SR edge in KSPEC-009 must match the "Linked requirements" field of §6.
3. The UR → SR edges consumed by KSPEC-009 come from the Source fields of KSPEC-003;
   the THR ↔ SR and ORK ↔ SR edges come from KSPEC-006 §7 and KSPEC-007 §risk-mapping
   respectively. This document adds the terminal VT hop only.
4. Coverage statistics in KSPEC-009 must be recomputed from §7 when either document is
   revised; divergent counts are a review finding.

---

## 9. Open items and TBD dependencies

| Item | Affects | Note |
|---|---|---|
| SR-P.003 latency budget TBD | VT-055 | Latencies recorded observationally only; no pass/fail until a budget is approved. |
| SR-F-EST.011 pending-trigger matrix TBD | VT-012 | Test waits for approved per-backend determinism matrix. |
| SR-F-REG.007 abuse-control threshold TBD | VT-020 | Flood threshold untestable until fixed. |
| SR-A.006 secret-length rule pending confirmation | VT-035 | ≥32-character rule inherited provisionally. |
| SR-S.012 CRL lifetime TBD / SR-C.008 stale-revocation policy TBD | VT-025/VT-028 follow-ups | Freshness assertions need approved lifetime policy. |
| SR-F-INT.008 joint OR.NET test vectors (Proposed) | VT-047 follow-ups | Interoperability vectors must be jointly agreed, never invented (tracker boundary). |
| SR-X.007 backup/restore procedure Partial | VT-053 | Drill deferred until the documented procedure is approved. |

---

*End of KSPEC-008, version 0.2 (Draft). This specification proposes tests; it records
executed results only where docs/IMPLEMENTATION-TRACKER.md records them, quoted
verbatim. No approval is recorded or inferred.*
