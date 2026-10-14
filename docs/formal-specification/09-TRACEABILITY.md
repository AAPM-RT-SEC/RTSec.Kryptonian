# Kryptonian Requirements Traceability Matrix

| Field | Value |
|---|---|
| Document title | Requirements Traceability Matrix (KSPEC-009) |
| Document ID | KSPEC-009 |
| Version | 0.2 (Draft) |
| Review status | Draft — pending review |
| Created | 2026-10-13 |
| Baseline repository commit | `682f996c8447afec13bc1fce5b7f59e6a06efa25` (branch `feat/windows-sandbox-installer`, "Release Windows gateway and portable enrollment demo 0.1.4") |
| Repository | https://github.com/AAPM-RT-SEC/RTSec.Kryptonian |
| Revision history | 0.1 — 2026-10-13 — Initial draft: bidirectional traceability UR-001 – UR-038 (KSPEC-002) → 154 SR-* (KSPEC-003 register) → architecture components (KSPEC-004) → implementation evidence (repository paths) → VT-001 – VT-060 (KSPEC-008); THR-001 – THR-018 (KSPEC-006) and ORK-001 – ORK-016 (KSPEC-007) linkages; gap lists (Author: V&V engineering team). |
| Source references | KSPEC-000 (conventions); KSPEC-002 (UR-001 – UR-038); KSPEC-003 (canonical Requirements Register, §21); KSPEC-004 (component inventory §3.2, findings §16.2–§16.3); KSPEC-006 (threat catalog §7, controls-to-requirements mapping §8); KSPEC-007 (risk register §3, requirement linkage §7.1); KSPEC-008 (VT-001 – VT-060); repository paths cited per KSPEC-000 §10 evidence source map |
| Author | Name: TBD — Date: TBD — Signature: TBD |
| Reviewer | Name: TBD — Date: TBD — Signature: TBD |
| Approver | Name: TBD — Date: TBD — Signature: TBD |
| Reconciliation revision | 0.2 - 2026-10-13 - source/document audit and publication controls (KSPEC-016) |

**Product classification (binding, KSPEC-000 §3).** Kryptonian is enterprise healthcare IT
cybersecurity infrastructure: an EST (RFC 7030) certificate enrollment, identity
provisioning, and credential lifecycle gateway. It is **not a regulated medical device**
and performs no clinical functions. No traceability entry in this document may be read as
claiming FDA classification, IEC 62304 lifecycle classification, ISO 14971 hazard coverage,
SDC conformity, an adopted EST profile, DICOM B.12/B.13 certification, or any other
standards-conformance status. Traceability covers **device identity, enrollment
authorization, network trust, and application-layer authorization only**; no gateway state
derives clinical capability.

**Evidence discipline (binding, KSPEC-000 §5 and §13).** Implementation-evidence citations
reuse the exact repository paths and provenance classes recorded in KSPEC-003 (canonical
register) and KSPEC-008 (VT evidence status). Nothing in this document upgrades a
provenance class, records a new test result, or converts a DOCUMENTED claim into VERIFIED.

## 1. Introduction

### 1.1 Purpose

This document (KSPEC-009) provides the bidirectional requirements traceability matrix for
the RTSec.Kryptonian gateway:

- **Stakeholder Need** (UR-nnn, KSPEC-002) → **System Requirement** (SR-*, KSPEC-003)
  → **Architecture Component** (KSPEC-004) → **Implementation Evidence** (repository
  paths, with KSPEC-000 §5 provenance classes) → **Verification Test** (VT-nnn, KSPEC-008),
  plus **security threat** (THR-nnn, KSPEC-006) and **operational risk** (ORK-nnn,
  KSPEC-007) linkages.
- The reverse direction (VT → SR → UR) is what the matrices expose; the gap lists of §9
  identify every cell that is empty and why.

### 1.2 Scope and method

- The SR ID space, short titles, statuses, priorities, and UR→SR edges are taken **only**
  from the canonical Requirements Register of KSPEC-003 (§21: 154 SRs — Existing 108,
  Partial 25, Missing 18, Proposed 3) and the per-requirement `Source` fields of KSPEC-003.
- The VT ID space and VT→SR / VT→THR / VT→ORK edges are taken **only** from KSPEC-008
  (VT-001 – VT-060; all 60 test records verified present and well-formed).
- The THR ID space and threat→SR prefix linkage intent are taken from KSPEC-006 (§7
  catalog, §8 mapping, prefix-level per §8); the ORK ID space and risk→SR area linkage
  intent are taken from KSPEC-007 (§3 register, §7.1).
- Architecture component names and repository paths are taken from KSPEC-004 (§3.2
  component inventory) and the KSPEC-003 `Evidence` fields; no new paths are asserted.
- Identifier consistency was checked against KSPEC-002, KSPEC-003, KSPEC-006, KSPEC-007,
  and KSPEC-008 while building every table; no requirement, test, threat, or risk ID is
  used here that does not exist in its source document (§10).

### 1.3 Reading conventions

- `—` means "no linkage of that kind is recorded in the source documents" (an honest
  empty cell, listed in §9 when it indicates a coverage gap).
- `Partial` / `Missing` / `Proposed` after an ID is that requirement's **status** in the
  KSPEC-003 register, never an invented classification.
- Verification method codes: T = Test, I = Inspection, A = Analysis, D = Demonstration.

## 2. Traceability model

### 2.1 Chain and anchor documents

```
KSPEC-001 ──elaborated by──> KSPEC-002 (UR-nnn)
KSPEC-002 ──elaborated by──> KSPEC-003 (SR-*)          [canonical register, §21]
KSPEC-003 ──realized by─────> KSPEC-004 (components)   [§3.2 inventory]
KSPEC-003/004 ──evidenced by─> repository paths        [KSPEC-003 Evidence fields]
KSPEC-003 ──verified by─────> KSPEC-008 (VT-nnn)
KSPEC-006/007 ──mitigated/monitored by──> KSPEC-003/008 (THR/ORK → SR/VT)
```

KSPEC-003 is the normative anchor for SR IDs; KSPEC-008 is the anchor for VT IDs;
KSPEC-006 and KSPEC-007 register THR and ORK IDs respectively and delegate exact SR
binding to this document (KSPEC-006 §8 and KSPEC-007 §7.1 record prefix-level intent
pending this binding).

### 2.2 Direction rules

- **Forward (need → evidence):** §5–§6 answer "which code path and which test shows this
  stakeholder need is met?" A UR is **covered** when at least one SR traces to it and at
  least one SR in the chain is linked to a VT; **fully covered** when every SR in the
  chain is VT-linked; otherwise **partially covered** (untested SRs named in §5).
- **Reverse (evidence → need):** every VT cites its linked SRs; §8 aggregates SR→VT
  coverage and §9.2 lists the SRs with no direct VT link.
- **Security/risk linkage:** THR and ORK cells in the area matrices carry the exact IDs
  recorded in KSPEC-008 VT records and, where KSPEC-006 §8 / KSPEC-007 §7.1 record
  prefix-level intent, the resolved specific SR IDs (§7).

### 2.3 Coverage semantics for gap accounting

| Flag | Meaning |
|---|---|
| Full | Every SR in the UR's chain is linked to at least one VT. |
| Partial | At least one chain SR is VT-linked but at least one is not (untested SRs named), or the only VTs are Proposed-status coverage. |
| None | No SR in the chain is linked to any specified VT. |

## 3. Architecture component reference (KSPEC-004 §3.2)

| Component | Layer | Responsibility (abridged) |
|---|---|---|
| `RTSec.Kryptonian.Api` | Host | Controllers (Est, Devices, Certificates, Crl, CaBackends, EstProfiles, Users, Auth, ApiKeys, NotificationSettings, GatewaySettings, Logs, Status, AcmeChallenge), Kestrel HTTPS + client-cert listeners, rate limiting, `CertificateExpiryWatcher`, `SandboxBootstrap`, middleware (`CertificateForwardingGuard`, CRL-only HTTP, plain-HTTP 426) |
| `RTSec.Kryptonian.Application` | Application | `EnrollmentOrchestrator`, `EnrollmentCertificatePolicy`, `CertificateRevocationService`, device/profile/backend/user/notification/gateway-settings services, `NotificationDispatcher` |
| `RTSec.Kryptonian.Domain` | Domain | Entities, enums (`DeviceStatus`, `CertificateStatus`, `EnrollmentStatus`, `CaBackendType`, `UserRole`, `RevocationReason`, `SmtpTlsMode`), value objects, `ICaConnector` interfaces |
| `RTSec.Kryptonian.Infrastructure` | Infrastructure | EF Core persistence (SQLite/Npgsql), repositories + unit of work, `SchemaUpgrades`, PKCS services, self-signed CA, harness connectors (ADCS SCEP, EJBCA REST), ACME via Certes, data protection, MailKit SMTP |
| `RTSec.Kryptonian.Web` | Presentation | Blazor Server admin dashboard calling the admin API |
| `RTSec.Kryptonian.Ui` | Presentation | React/TypeScript admin dashboard (Vite) served as static files by the API |
| `Kryptonian.MedicalDevice` | Demonstration | WPF device simulator with EST enrollment client (legacy activation headers for compatibility) |
| `Kryptonian.DICOMTls` | Demonstration | DIMSE-TLS demonstration terminating mTLS; issuer, TLS purpose, online revocation |
| `RTSec.Kryptonian.DimseTlsProxy` | Demonstration | Standalone DIMSE TLS proxy; bounded connection lifetimes; atomic PFX reload |
| `RTSec.Kryptonian.CaHarness` | Test/demo | Stand-in CA backend used by harness connectors and ACME relay/challenge flows |

### 3.1 Area-to-component shorthand used in §4

| Area (KSPEC-003) | Primary KSPEC-004 components |
|---|---|
| SR-F-REG | Api/DevicesController; Application/DeviceService, EnrollmentOrchestrator; Domain/DeviceStatus; Infrastructure/EnrollmentEventRepository |
| SR-F-EST | Api/EstController, Program.cs (Kestrel, 426 middleware); Application/EnrollmentOrchestrator, EnrollmentCertificatePolicy; Infrastructure/PkcsService |
| SR-F-CLM | Api/CertificatesController, CrlController, CertificateExpiryWatcher; Application/CertificateRevocationService, EnrollmentOrchestrator; Infrastructure/PkcsService, UnitOfWork, MailKitSmtpSender |
| SR-F-CAI | Api/CaBackendsController; Domain/ICaConnector; Infrastructure/CaConnectorFactory, Harness/*, Acme/*, SelfSignedCaConnector |
| SR-F-ADM | Api controllers (Status, Users, Auth, ApiKeys, NotificationSettings, GatewaySettings, Logs, EstProfiles); Web; Ui |
| SR-F-INT | Api/EstController; OpenAPI.*.yaml; Kryptonian.MedicalDevice; Kryptonian.DICOMTls; DimseTlsProxy |
| SR-S | Api/Program.cs, Middleware/CertificateForwardingGuard; Application/AuthService; Infrastructure/DataProtectionService, KryptonianDbContext |
| SR-A | Api/Program.cs (auth schemes), Authentication/ApiKeyAuthenticationHandler, AuthController, EstController |
| SR-Z | Domain/UserRole; Api/Program.cs (policies); controller `[Authorize]`/policy attributes |
| SR-C | Domain/EstProfile; Application/EnrollmentOrchestrator, CertificateRevocationService; Api/CrlController; Infrastructure/SchemaUpgrades |
| SR-D | Infrastructure/KryptonianDbContext, SchemaUpgrades, repositories, UnitOfWork; Api/Program.cs (Data Protection) |
| SR-P | Api/Program.cs (rate limiting, caching); Api/DevicesController; Infrastructure Npgsql path |
| SR-R | Api/CertificateExpiryWatcher; Application/NotificationDispatcher; Infrastructure/UnitOfWork |
| SR-V | Api/StatusController; Api/Program.cs (health checks); persistence layer |
| SR-U | Api/Logging/LiveLogBroadcaster, LogsController; Infrastructure/EnrollmentEventRepository; Serilog pipeline |
| SR-B | Api/Program.cs, SandboxBootstrap; docker-compose*.yml; deploy/; installer/windows/ |
| SR-O | Infrastructure/CaConnectorFactory; Api/Program.cs (rolling file sink); .github/workflows/ |
| SR-X | Application/CertificateRevocationService; Api/Program.cs (CrlOnlyHttp); Api/EstController; Kryptonian.DICOMTls |

## 4. SR traceability matrices by requirement area

Columns: **UR** = Source UR(s) per KSPEC-003; **Component** = §3.1 shorthand; **Evidence** =
repository paths with provenance per KSPEC-003 `Evidence` fields (C = CODE-OBSERVED,
V = VERIFIED, D = DOCUMENTED); **VT** = linked verification tests per KSPEC-008;
**THR** / **ORK** = exact IDs recorded in KSPEC-008 VT records (plus prefix-level intent
from KSPEC-006 §8 / KSPEC-007 §7.1, resolved in §7); **Status** = KSPEC-003 register status.

### 4.1 Device registration and identity management (SR-F-REG)

| SR ID | Short title | UR | Component | Implementation evidence | VT | THR | ORK | Status |
|---|---|---|---|---|---|---|---|---|
| SR-F-REG.001 | Device registration with activation code | UR-001 | REG | `src/RTSec.Kryptonian.Api/Controllers/DevicesController.cs` C; `src/RTSec.Kryptonian.Application/Services/DeviceService.cs` C; docs/device-enrollment.md D | VT-014 | THR-003 | — | Existing |
| SR-F-REG.002 | Activation code one-time use, replay rejected | UR-002 | REG | `src/RTSec.Kryptonian.Api/Controllers/EstController.cs` C; `DeviceService.cs` C; replay rejection V (tracker, `artifacts/developer/verify-*/evidence.json`) | VT-015, VT-048, VT-060 | THR-003, THR-006 | ORK-005 | Existing |
| SR-F-REG.003 | Centralized device inventory | UR-003 | REG | `DevicesController.cs` (List, Get, Events, AllCertificates) C; `src/RTSec.Kryptonian.Application/DTOs/DeviceDto.cs` C; README.md D | VT-014 | THR-003 | — | Existing |
| SR-F-REG.004 | Device record status lifecycle | UR-003 | REG | `src/RTSec.Kryptonian.Domain/Enums/DeviceStatus.cs` C; `DevicesController.cs` (Events) C | VT-019 | — | — | Existing |
| SR-F-REG.005 | Enrollment identity binding | UR-020 | REG | `src/RTSec.Kryptonian.Application/Services/EnrollmentOrchestrator.cs`, `EnrollmentCertificatePolicy.cs` C; PR 38 binding V (tracker) | VT-016 | THR-001 | — | Existing |
| SR-F-REG.006 | Self-service requests create pending records only | UR-001 | REG | `DevicesController.cs` (RequestApproval, `[AllowAnonymous]`, 202) C | VT-017 | THR-002 | — | Existing |
| SR-F-REG.007 | Anonymous endpoint abuse controls | — (KSPEC-005 open item 6; ORK flooding exposure) | REG | `DevicesController.cs` (RequestApproval, no rate-limit attribute) C; `src/RTSec.Kryptonian.Api/appsettings.json` (IpRateLimiting scoped to EST paths) C | VT-020 | THR-013 | — | Missing |
| SR-F-REG.008 | Trust removal blocks enrollment | UR-009 (UC-04, SCN-05) | REG | `EnrollmentOrchestrator.cs` (trust-policy gate) C; docs/flow.md D | VT-018 | THR-012 | ORK-005 | Existing |
| SR-F-REG.009 | Device identity metadata preservation | UR-003, UR-020 | REG | `DeviceDto.cs` C; README.md D | VT-014 | THR-003 | — | Existing |
| SR-F-REG.010 | History preserved on device removal | UR-011 | REG | `src/RTSec.Kryptonian.Infrastructure/Repositories/EnrollmentEventRepository.cs` C; `DevicesController.cs` (Events after removal) C | VT-014 | THR-003 | — | Existing |

### 4.2 EST protocol behavior (SR-F-EST)

| SR ID | Short title | UR | Component | Implementation evidence | VT | THR | ORK | Status |
|---|---|---|---|---|---|---|---|---|
| SR-F-EST.001 | EST endpoint surface | UR-016, UR-022 | EST | `src/RTSec.Kryptonian.Api/Controllers/EstController.cs` (routes) C; docs/ARCHITECTURE.md D; independent-client EST V (tracker) | VT-001 | THR-002 | — | Existing |
| SR-F-EST.002 | cacerts response format | UR-017 | EST | `EstController.cs` (GetCaCerts, Pkcs7MimeType) C; `src/RTSec.Kryptonian.Infrastructure/Crypto/PkcsService.cs` C | VT-002 | THR-012 | — | Existing |
| SR-F-EST.003 | CSR acceptance and 64 KiB limit | UR-016, UR-023 | EST | `EstController.cs` (SimpleEnroll, `RequestSizeLimit`, 413) C; `PkcsService.cs` (DecodeEstRequestBodyAsync) C | VT-003, VT-044 | THR-004, THR-013 | — | Existing |
| SR-F-EST.004 | Successful enrollment response format | UR-016, UR-018 | EST | `EstController.cs` (HandleEnrollmentResult) C; executed Schannel mTLS renewal V (tracker) | VT-004, VT-044 | THR-005 | — | Existing |
| SR-F-EST.005 | Unknown profile label 404, no fallback | UR-022 | EST | `EstController.cs` (ResolveProfileIdAsync, 404) C; PR 37 V (tracker) | VT-005 | THR-012 | — | Existing |
| SR-F-EST.006 | Basic bootstrap authentication over TLS | UR-002, UR-019 | EST | `EstController.cs` (Basic parsing; 403 without TLS) C; independent curl HTTP-Basic EST over real HTTPS V (tracker) | VT-006 | THR-003 | ORK-005 | Existing |
| SR-F-EST.007 | Legacy headers compatibility only | UR-019 (§16.3; EXC-004) | EST | `EstController.cs` (X-Activation-Code fallback) C; tracker boundary D; no disable switch exists C | VT-011 | — | — | Partial |
| SR-F-EST.008 | Re-enroll requires native TLS client cert | UR-021 (UC-03, SCN-03) | EST | `EstController.cs` (SimpleReenroll, client-cert 401) C; renewal V (tracker) | VT-007 | THR-006 | — | Existing |
| SR-F-EST.009 | Client certificate validation on EST | UR-008, UR-021 | EST | `EstController.cs` (ValidateClientCertificateAsync) C; `src/RTSec.Kryptonian.Api/Authentication/ConfiguredClientCertificateTrust.cs` C | VT-008 | THR-001 | ORK-008 | Existing |
| SR-F-EST.010 | Pending 202 + Retry-After | UR-023 | EST | `EstController.cs` (202 + Retry-After) C; docs/API-GUIDE.md D | VT-009 | — | — | Existing |
| SR-F-EST.011 | Deterministic pending semantics per backend | UR-023; KSPEC-005 ICD-EST.004 open item (UNKNOWN) | EST | `EnrollmentOrchestrator.cs` (IsPending/RetryAfterSeconds) C; docs/formal-specification/05-INTERFACE-CONTROL.md §6 open item D | VT-012 | — | — | Missing |
| SR-F-EST.012 | Plain-HTTP EST refused (426) | UR-002 | EST | `src/RTSec.Kryptonian.Api/Program.cs` (426 middleware, `AllowPlainHttp` default false) C; `appsettings.json` C | VT-010 | THR-013 | — | Existing |
| SR-F-EST.013 | Distinguishable error semantics | UR-023 | EST | `EstController.cs` (EstError status mapping) C; docs/API-GUIDE.md D; docs/medical-device-demo.md D | VT-012, VT-044 | THR-004, THR-013 | — | Existing |
| SR-F-EST.014 | CSR attributes (`csrattrs`) endpoint | — (KSPEC-004 P-3) | EST | `EstController.cs` (no csrattrs route) C; docs/formal-specification/04-SOFTWARE-DESIGN.md P-3 D | VT-013 | THR-004 | — | Missing |

### 4.3 Certificate lifecycle management flows (SR-F-CLM)

| SR ID | Short title | UR | Component | Implementation evidence | VT | THR | ORK | Status |
|---|---|---|---|---|---|---|---|---|
| SR-F-CLM.001 | Approval gates enrollment | UR-001, UR-008 | CLM | `DevicesController.cs` (Approve, DeviceAdmin policy) C; `EnrollmentOrchestrator.cs` (status gate) C | VT-021 | THR-002 | ORK-004 | Existing |
| SR-F-CLM.002 | Automatic renewal without re-approval | UR-021, UR-025 | CLM | `EnrollmentOrchestrator.cs` (ReenrollAsync) C; renewal V (tracker) | VT-022 | THR-011 | ORK-001 | Existing |
| SR-F-CLM.003 | Renewal: new key, preserved identity | UR-021 | CLM | renewal V (tracker); `EnrollmentCertificatePolicy.cs` C | VT-023 | THR-006 | ORK-001, ORK-015 | Existing |
| SR-F-CLM.004 | Certificate expiry visibility | UR-024 | CLM | `src/RTSec.Kryptonian.Api/Controllers/CertificatesController.cs` C; `DevicesController.cs` (AllCertificates) C; README.md D | VT-029 | THR-011 | ORK-001, ORK-007, ORK-014 | Existing |
| SR-F-CLM.005 | Automated expiry scanning and notification | UR-012, UR-024 | CLM | `src/RTSec.Kryptonian.Api/HostedServices/CertificateExpiryWatcher.cs` (24 h scan, 7-day re-notify) C; `src/RTSec.Kryptonian.Application/Notifications/NotificationDispatcher.cs` C | VT-029 | THR-011 | ORK-001, ORK-007, ORK-014 | Partial |
| SR-F-CLM.006 | Non-renewal alerting | UR-012 | CLM | `CertificateExpiryWatcher.cs` C; `src/RTSec.Kryptonian.Infrastructure/Notifications/MailKitSmtpSender.cs` C; docs/email-notifications.md D; delivery not exercised D (tracker) | VT-029 | THR-011 | ORK-007, ORK-014 | Partial |
| SR-F-CLM.007 | Authorized revocation with issuer-scoped signed CRLs | UR-010 (UC-04, SCN-05) | CLM | `src/RTSec.Kryptonian.Application/Services/CertificateRevocationService.cs` C; `src/RTSec.Kryptonian.Api/Controllers/CrlController.cs` C; X509Chain Revoked V (tracker) | VT-025 | THR-010 | ORK-008 | Existing |
| SR-F-CLM.008 | Transactional revocation persistence | UR-010 | CLM | `CertificateRevocationService.cs` C; `src/RTSec.Kryptonian.Infrastructure/Data/UnitOfWork.cs` C; docs/formal-specification/07-OPERATIONAL-RISK.md D | VT-026 | THR-010 | ORK-008 | Existing |
| SR-F-CLM.009 | Revoked devices denied renewal | UR-009, UR-010 | CLM | `EnrollmentOrchestrator.cs` (trust/status gate) C; proxy tests rejecting revoked clients V (tracker) | VT-027 | THR-010 | — | Existing |

### 4.4 CA backend integration (SR-F-CAI)

| SR ID | Short title | UR | Component | Implementation evidence | VT | THR | ORK | Status |
|---|---|---|---|---|---|---|---|---|
| SR-F-CAI.001 | Multi-backend configuration without code changes | UR-004 | CAI | `src/RTSec.Kryptonian.Api/Controllers/CaBackendsController.cs` C; `src/RTSec.Kryptonian.Application/Services/CaBackendService.cs` C; docs/ca-backend-configuration.md D | VT-040 | — | ORK-003, ORK-010 | Existing |
| SR-F-CAI.002 | Backend testing and profile-bound routing | UR-005 | CAI | `CaBackendsController.cs` (test/activation endpoints) C; docs/ca-backend-configuration.md D | VT-040 | — | ORK-003, ORK-010 | Existing |
| SR-F-CAI.003 | Pluggable connector abstraction | UR-030, UR-038 | CAI | `src/RTSec.Kryptonian.Infrastructure/Crypto/CaConnectorFactory.cs` C; `src/RTSec.Kryptonian.Infrastructure/Harness/AdcsCaConnector.cs`, `EjbcaCaConnector.cs`, `SelfSignedEstCaConnector.cs` C; `src/RTSec.Kryptonian.Infrastructure/Acme/AcmeCaConnector.cs` C | VT-043 | THR-005, THR-008 | ORK-003, ORK-008 | Existing |
| SR-F-CAI.004 | Transparent CA protocol mapping | UR-018 | CAI | `EnrollmentOrchestrator.cs` (backend dispatch) C; docs/flow.md D | VT-040, VT-045 | — | ORK-003 | Existing |
| SR-F-CAI.005 | ADCS integration via configurable SCEP endpoint | UR-030; docs/adcs-real-backend-demo.md Gap 1 | CAI | `src/RTSec.Kryptonian.Infrastructure/Harness/AdcsCaConnector.cs`, `ScepClient.cs` C; docs/adcs-real-backend-demo.md D | VT-043 | THR-005, THR-008 | ORK-003, ORK-008 | Partial |
| SR-F-CAI.006 | EJBCA REST integration with honest revocation | UR-010, UR-030 | CAI | `src/RTSec.Kryptonian.Infrastructure/Harness/EjbcaCaConnector.cs` C; hospital revocation unverified D (tracker) | VT-043 | THR-005, THR-008 | ORK-003, ORK-008 | Partial |
| SR-F-CAI.007 | ACME enrollment with functional challenge set | UR-030; KSPEC-004 Dns01ChallengeStub | CAI | `src/RTSec.Kryptonian.Infrastructure/Acme/AcmeCaConnector.cs`, `Http01ChallengeStore.cs`, `Dns01ChallengeStub.cs` C | VT-042 | THR-005 | — | Partial |
| SR-F-CAI.008 | Gateway CRL generation fail-closed for incapable backends | UR-010 | CAI | `CertificateRevocationService.cs`, `CrlController.cs` (501 on unsupported CRL generation) C; docs/formal-specification/07-OPERATIONAL-RISK.md D | VT-041 | THR-008 | ORK-008 | Existing |
| SR-F-CAI.009 | CA material persistence across restarts and upgrades | UR-037 | CAI | restart on populated SQLite V (tracker); `src/RTSec.Kryptonian.Infrastructure/Repositories/CaBackendRepository.cs` C | VT-041 | THR-008 | ORK-008 | Existing |
| SR-F-CAI.010 | Secure connector credential handling | UR-013 (**Missing** in KSPEC-002) | CAI | `appsettings.json`, `Program.cs` (configuration/environment sourcing) C; plain config at baseline D (tracker) | — | — | — | Missing |

### 4.5 Administration and portal (SR-F-ADM)

| SR ID | Short title | UR | Component | Implementation evidence | VT | THR | ORK | Status |
|---|---|---|---|---|---|---|---|---|
| SR-F-ADM.001 | Administrative portal surface | UR-001, UR-003, UR-004, UR-015, UR-024 | ADM | `src/RTSec.Kryptonian.Api/Controllers/` (15 controllers) C; `src/RTSec.Kryptonian.Web` C; `src/RTSec.Kryptonian.Ui` C; README.md D | — | — | — | Existing |
| SR-F-ADM.002 | Gateway dashboard summary | UR-006 | ADM | `src/RTSec.Kryptonian.Api/Controllers/StatusController.cs` C; README.md D | — | — | — | Existing |
| SR-F-ADM.003 | User account lifecycle with lockout protection | UR-007, UR-015 | ADM | `src/RTSec.Kryptonian.Api/Controllers/UsersController.cs` C; `src/RTSec.Kryptonian.Application/Services/UserService.cs` (self-deletion, last-admin guards) C; docs/user-management.md D | VT-037 | THR-007, THR-009 | — | Existing |
| SR-F-ADM.004 | API key lifecycle | UR-027 | ADM | `src/RTSec.Kryptonian.Api/Controllers/ApiKeysController.cs` C; `src/RTSec.Kryptonian.Infrastructure/Repositories/ApiKeyRepository.cs` C; docs/user-management.md D | VT-039 | THR-007 | — | Existing |
| SR-F-ADM.005 | Initial administrator bootstrap | UR-007, UR-015 | ADM | docs/admin-setup.md D; docs/user-management.md (FAQ) D | — | THR-009 | — | Existing |
| SR-F-ADM.006 | Notification settings management | UR-012 | ADM | `NotificationSettingsController.cs` (SystemAdmin policies, test endpoint) C; `src/RTSec.Kryptonian.Application/Services/NotificationSettingsService.cs` C; docs/email-notifications.md D | — | — | ORK-014 | Existing |
| SR-F-ADM.007 | EST profile management with additive trust-list updates | UR-031, UR-032 | ADM | `src/RTSec.Kryptonian.Api/Controllers/EstProfilesController.cs` C; `EstProfileService.cs` C; `src/RTSec.Kryptonian.Infrastructure/Data/SchemaUpgrades.cs` (EnsureTrustPolicyAsync) C | — | THR-012 | — | Existing |
| SR-F-ADM.008 | Gateway settings management | UR-006, UR-024 | ADM | `GatewaySettingsController.cs` (SystemAdmin) C; `GatewaySettingsService.cs` C; `GatewaySettingsRepository.cs` C | — | — | ORK-010 | Existing |
| SR-F-ADM.009 | Live activity feed | UR-006, UR-011 | ADM | `src/RTSec.Kryptonian.Api/Controllers/LogsController.cs` C; `src/RTSec.Kryptonian.Api/Logging/LiveLogBroadcaster` (registered in `Program.cs`) C | — | — | — | Existing |
| SR-F-ADM.010 | Health and status endpoint | UR-029 | ADM/V | `StatusController.cs` C; docs/DEPLOYMENT.md D | VT-056 | — | ORK-010, ORK-013 | Existing |

### 4.6 Interfaces and interoperability demonstrations (SR-F-INT)

| SR ID | Short title | UR | Component | Implementation evidence | VT | THR | ORK | Status |
|---|---|---|---|---|---|---|---|---|
| SR-F-INT.001 | Device-side EST client without gateway-specific code | UR-019 (executed evidence) | INT | independent curl HTTP Basic EST over real HTTPS V (tracker); docs/API-GUIDE.md (EstClient example) D | — | — | — | Existing |
| SR-F-INT.002 | Legacy headers demo-only, never a new requirement | UR-019 (§16.3) | INT | `EstController.cs` (Basic path independent of legacy headers) C; tracker D | VT-011 | — | — | Existing |
| SR-F-INT.003 | Authoritative OpenAPI admin contract | UR-027; `OpenAPI.Client.yaml` empty, `OpenAPI.Schema.yaml` stale | INT | `OpenAPI.Client.yaml` (empty), `OpenAPI.Schema.yaml` (stale) C; `OpenAPI.Admin.yaml`, `OpenAPI.ESTProfiles.yaml`, `OpenAPI.StatusObserve.yaml` C; docs/formal-specification/05-INTERFACE-CONTROL.md D | — | — | — | Missing |
| SR-F-INT.004 | EST profile schema currency | UR-031 | INT | `OpenAPI.ESTProfiles.yaml` C; `src/RTSec.Kryptonian.Application/DTOs/EstProfileDto.cs` C; docs/formal-specification/05-INTERFACE-CONTROL.md D | — | — | — | Partial |
| SR-F-INT.005 | DICOM mTLS demonstration (enroll, use, rotate, deny) | UR-034, UR-036 | INT | `src/Kryptonian.DICOMTls/` C; two mutual-TLS DICOM C-STORE transfers with SOP Instance UID comparison V (tracker, `artifacts/developer/verify-*/evidence.json`); `tests/RTSec.Kryptonian.DimseTlsProxy.Tests/` C | VT-047 | THR-001 | ORK-002 | Existing |
| SR-F-INT.006 | Demonstration clients never presented as product deliverables | UR-026, UR-035 | INT | `src/Kryptonian.MedicalDevice/`, `src/Kryptonian.DICOMTls/` C; README.md disclaimer D; tracker boundaries D | — | — | — | Existing |
| SR-F-INT.007 | No second enrollment protocol; OR.NET alignment without conformity | UR-034 | INT | README.md D; tracker boundaries D (no SDC conformity claimed); docs/ORNET-ALIGNMENT-2026-09-09.md D | VT-011 | — | — | Existing |
| SR-F-INT.008 | Independent client/server pairs at the real network boundary | UR-036 | INT | `artifacts/developer/verify-*/evidence.json` (gateway-boundary verifier) V; `tests/` (8 test projects) C; OR.NET joint vectors not present PROPOSED | VT-058 | THR-014 | ORK-016 | Proposed |

### 4.7 Security requirements (SR-S)

| SR ID | Short title | UR | Component | Implementation evidence | VT | THR | ORK | Status |
|---|---|---|---|---|---|---|---|---|
| SR-S.001 | TLS-only credential exchanges | UR-002, UR-014 | S | `src/RTSec.Kryptonian.Api/Program.cs` (426 middleware; redirect default outside Development) C; docs/DEPLOYMENT.md D | VT-031 | THR-003, THR-016 | — | Existing |
| SR-S.002 | CSR and returned-certificate policy enforcement | UR-033 (V in KSPEC-002) | S | `src/RTSec.Kryptonian.Application/Services/EnrollmentCertificatePolicy.cs` C; PR 43 V (tracker) | VT-032 | THR-004, THR-005 | — | Existing |
| SR-S.003 | Gateway-owned trust-policy gate | UR-008 | S | `EnrollmentOrchestrator.cs` (pre-CA trust gate) C; docs/flow.md D | VT-033 | THR-012 | ORK-005 | Existing |
| SR-S.004 | Forwarded-certificate guard | UR-008; PR 39 | S | `src/RTSec.Kryptonian.Api/Middleware/CertificateForwardingGuard.cs` (KnownProxies, 403/400, default empty = fail closed) C; direct spoof rejected V (tracker) | VT-034 | THR-017 | — | Existing |
| SR-S.005 | Authenticated forwarder identity | — (KSPEC-004 P-2; THR-017 XFCC spoofing route) | S | `CertificateForwardingGuard.cs` (IP-set membership check only) C; docs/formal-specification/04-SOFTWARE-DESIGN.md P-2 D | VT-034 | THR-017 | — | Missing |
| SR-S.006 | EST rate-limit capacity | UR-014 | S | `Program.cs` (UseIpRateLimiting) C; `appsettings.json` (IpRateLimiting rules) C; docs/ARCHITECTURE.md D | VT-055 | THR-013 | — | Existing |
| SR-S.007 | No hard-coded signing secrets | UR-013; `Program.cs` dev fallback finding | S | `Program.cs` (jwtSecret fallback constant) C; docs/formal-specification/05-INTERFACE-CONTROL.md (ICD-CFG.002 finding) D | VT-035 | THR-007, THR-009, THR-016 | — | Missing |
| SR-S.008 | Secrets at rest | UR-013 (**Missing** in KSPEC-002) | S | `src/RTSec.Kryptonian.Infrastructure/Data/KryptonianDbContext.cs` C; `src/RTSec.Kryptonian.Infrastructure/Security/DataProtectionService.cs` C (field coverage not evidenced); plain-config storage D (tracker) | — | THR-016 | — | Missing |
| SR-S.009 | Password hashing strength | UR-015 | S | BCrypt.Net-Next in `src/RTSec.Kryptonian.Api/RTSec.Kryptonian.Api.csproj` C; `AuthService.cs`, `UserService.cs` C; plans/user-management.md D | VT-036 | THR-009 | — | Existing |
| SR-S.010 | Data Protection key ring management | — (sandbox behavior; windows-installation fail-closed) | S | `Program.cs` (AddDataProtection, PersistKeysToFileSystem) C; `DataProtectionService.cs` C; artifacts/windows-installer/payload/docs/windows-installation.md D | — | THR-016 | ORK-015 | Partial |
| SR-S.011 | Device private key non-archival | — (KSPEC-004 A-2; UR-013 rationale) | S | `src/RTSec.Kryptonian.Domain/Entities/` (`EncryptedPrivateKeyPem` field) C; docs/formal-specification/04-SOFTWARE-DESIGN.md A-2 D | — | THR-016 | — | Proposed |
| SR-S.012 | Configurable CRL lifetime | — (KSPEC-004 A-4; UR-010) | S | 60 s lifetime C in `src/RTSec.Kryptonian.Infrastructure/Crypto/SelfSignedCaConnector.cs` and CRL issuance path; docs/formal-specification/04-SOFTWARE-DESIGN.md A-4 D | — | THR-010 | ORK-009 | Missing |
| SR-S.013 | CORS restricted to the admin UI | UR-015 | S | `Program.cs` (KryptonianUi CORS from `Kryptonian:Ui:AllowedOrigins`) C; `appsettings.json` C | VT-031 | THR-003, THR-016 | — | Existing |
| SR-S.014 | Dependency vulnerability management | — (PR 52; tracker advisories) | S | `.github/workflows/` (CI advisory gate) C; tracker (npm audit to zero; transitive highs remain) D | VT-058 | THR-014 | ORK-016 | Partial |

### 4.8 Authentication requirements (SR-A)

| SR ID | Short title | UR | Component | Implementation evidence | VT | THR | ORK | Status |
|---|---|---|---|---|---|---|---|---|
| SR-A.001 | Authenticated access to admin API (JWT + API key schemes) | UR-015, UR-027 | A | `src/RTSec.Kryptonian.Api/Program.cs` (PolicyScheme forwarding) C; `src/RTSec.Kryptonian.Api/Authentication/ApiKeyAuthenticationHandler.cs` C | VT-037 | THR-007, THR-009 | — | Existing |
| SR-A.002 | Bounded JWT session lifetime | — (docs/user-management.md 8-hour sessions) | A | `Program.cs` (TokenValidationParameters, ClockSkew) C; docs/user-management.md D; 8-hour default D (docs/formal-specification/05-INTERFACE-CONTROL.md) | VT-037 | THR-009 | — | Existing |
| SR-A.003 | API key hashing and status checks | UR-027 | A | `ApiKeyAuthenticationHandler.cs` (SHA-256 hash lookup, RevokedAt/ExpiresAt/IsActive, LastUsedAt) C | VT-039 | THR-007 | — | Existing |
| SR-A.004 | Legacy static API keys disabled by default | — (docs/user-management.md; plans/user-management.md) | A | docs/user-management.md (legacy static keys) D; plans/user-management.md D | VT-039 | THR-007 | — | Existing |
| SR-A.005 | API keys carry the owner's role at generation | — (docs/user-management.md) | A | `ApiKeyAuthenticationHandler.cs` (role from ApiKey record) C; docs/user-management.md D | VT-039 | THR-007 | — | Existing |
| SR-A.006 | JWT signing secret provisioning | UR-013; hard-coded dev fallback in `Program.cs` | A | `Program.cs` (fallback constant; no fail-fast check) C; plans/user-management.md D | VT-035 | THR-007, THR-009, THR-016 | — | Missing |
| SR-A.007 | EST bootstrap 401 challenge over TLS | UR-023; PR 42 | A | `src/RTSec.Kryptonian.Api/Controllers/EstController.cs` (BasicUnauthorized with WWW-Authenticate) C; docs/formal-specification/05-INTERFACE-CONTROL.md (401 challenge row) D | VT-006 | THR-003 | ORK-005 | Existing |
| SR-A.008 | Enrollment identity from client certificate, not headers | — (PR 38; EstController device-identifier rule) | A | `EstController.cs` (GetDeviceIdentifier returns null for header; certificate subject used) C | VT-007, VT-023 | THR-006 | — | Existing |
| SR-A.009 | Development-only anonymous bypass gating | UR-015 | A | `ApiKeyAuthenticationHandler.cs` (bypass requires AllowAnonymousInDevelopment && no static keys && Development) C | — | THR-007 | — | Existing |
| SR-A.010 | Uniform authentication failure responses | — (docs/user-management.md login; THR-009 model input) | A | `src/RTSec.Kryptonian.Api/Controllers/AuthController.cs` (uniform "Invalid username or password.") C | VT-037 | THR-007, THR-009 | — | Existing |

### 4.9 Authorization requirements (SR-Z)

| SR ID | Short title | UR | Component | Implementation evidence | VT | THR | ORK | Status |
|---|---|---|---|---|---|---|---|---|
| SR-Z.001 | Three-role authorization model | UR-015 (Critical) | Z | `src/RTSec.Kryptonian.Domain/Enums/UserRole.cs` (Standard, DeviceAdmin, SystemAdmin) C; `Program.cs` (StandardOrAbove, DeviceAdmin, SystemAdmin policies) C | VT-038 | THR-007 | — | Existing |
| SR-Z.002 | DeviceAdmin on device/certificate mutations | UR-015 (UC-08) | Z | `DevicesController.cs` (DeviceAdmin on mutations) C; `CertificatesController.cs` (controller-level DeviceAdmin) C | VT-038 | THR-007 | — | Existing |
| SR-Z.003 | SystemAdmin on configuration/user administration | UR-015 | Z | `CaBackendsController.cs`, `EstProfilesController.cs`, `UsersController.cs`, `NotificationSettingsController.cs`, `GatewaySettingsController.cs` (SystemAdmin on mutations) C | VT-038 | THR-007 | — | Existing |
| SR-Z.004 | Standard-or-above read access | UR-015 | Z | `Program.cs` (StandardOrAbove policy) C; controller-level `[Authorize]` on reads C | VT-038 | THR-007 | — | Existing |
| SR-Z.005 | Revocation/trust actions restricted | UR-009, UR-010 | Z | `CertificatesController.cs` (DeviceAdmin) C; `DevicesController.cs` (trust endpoints under DeviceAdmin) C | VT-038 | THR-007 | — | Existing |
| SR-Z.006 | API key role binding, no client claim ingestion | UR-027 | Z | `ApiKeyAuthenticationHandler.cs` (BuildTicket role from apiKey record) C | VT-039 | THR-007 | — | Existing |
| SR-Z.007 | Explicit role policy on all mutations | — (KSPEC-005 open item 4) | Z | `CaBackendsController.cs` (test endpoint `[Authorize]` without policy) C; docs/formal-specification/05-INTERFACE-CONTROL.md open items D | — | THR-007 | — | Partial |
| SR-Z.008 | Anonymous access limited to a documented set | — (controller survey; KSPEC-005 open item 6) | Z | controller `[Authorize]` coverage; single `[AllowAnonymous]` on device-requests C; `Program.cs` (unauthenticated health map) C | VT-020 | THR-013 | — | Existing |
| SR-Z.009 | Confirmed self-service authorization model | — (KSPEC-005 open item 6; docs/formal-specification/05-INTERFACE-CONTROL.md) | Z | `DevicesController.cs` (RequestApproval `[AllowAnonymous]`) C; docs/formal-specification/05-INTERFACE-CONTROL.md open item D | — | — | — | Proposed |
| SR-Z.010 | No clinical capability derivation | UR-026 (Critical); EXC-001/EXC-002 | Z | docs/CHANGES-AND-ORNET-ALIGNMENT.md (renewal ≠ recertification) D; README.md disclaimer D; docs/formal-specification/04-SOFTWARE-DESIGN.md boundary D | VT-038 | — | — | Existing |

### 4.10 Certificate lifecycle policy requirements (SR-C)

| SR ID | Short title | UR | Component | Implementation evidence | VT | THR | ORK | Status |
|---|---|---|---|---|---|---|---|---|
| SR-C.001 | Profile policy bundle | UR-031 (Critical) | C | `src/RTSec.Kryptonian.Domain/Entities/EstProfile.cs`, `src/RTSec.Kryptonian.Application/DTOs/EstProfileDto.cs` C; docs/ca-backend-configuration.md D | VT-030 | THR-004, THR-005 | ORK-004 | Existing |
| SR-C.002 | Trust policy durability across restarts and updates | UR-032 | C | restart persistence on populated SQLite V (tracker); partial-update preservation PR 47; `SchemaUpgrades.cs` C | VT-033 | THR-012 | ORK-005 | Existing |
| SR-C.003 | Profile-configured validity periods | UR-031; TBD-001 | C | `EstProfileDto.cs` (validity fields) C; docs/ca-backend-configuration.md D | VT-030 | THR-004, THR-005 | ORK-004 | Existing |
| SR-C.004 | Renewal window authorization | UR-021 | C | `EnrollmentOrchestrator.cs` (reenroll validation) C; docs/device-enrollment.md D | VT-022, VT-024 | THR-011 | ORK-001 | Existing |
| SR-C.005 | Issuer-scoped CRL publication | UR-010; PR 46 | C | `CertificateRevocationService.cs` C; `CrlController.cs` C | VT-025 | THR-010 | ORK-008 | Existing |
| SR-C.006 | CRL distribution points on issued certificates | UR-010; PR 46 | C | `CertificateRevocationService.cs`, `PkcsService.cs` C | VT-025 | THR-010 | ORK-008 | Existing |
| SR-C.007 | Online revocation checking at peers | UR-010; PR 45 | C | `ConfiguredClientCertificateTrust.cs` C; `src/Kryptonian.DICOMTls/` C; tracker D | VT-028, VT-047 | THR-001, THR-010 | ORK-008 | Existing |
| SR-C.008 | Bounded revocation propagation semantics | — (EXC-007; KSPEC-004 A-5) | C | tracker (bounded proxy lifetimes) D; docs/formal-specification/04-SOFTWARE-DESIGN.md A-5 D | — | THR-010 | — | Partial |
| SR-C.009 | Separation of revocation and enrollment denial | UR-009, UR-010; PR 46 | C | `CertificateRevocationService.cs` (separation) C | — | THR-010 | ORK-008 | Existing |
| SR-C.010 | Enrollment event record per issuance attempt | UR-011 | C | `EnrollmentEventRepository.cs` C; `src/RTSec.Kryptonian.Api/Controllers/StatusController.cs` (enrollments listing) C | VT-059 | — | — | Partial |

### 4.11 Data management requirements (SR-D)

| SR ID | Short title | UR | Component | Implementation evidence | VT | THR | ORK | Status |
|---|---|---|---|---|---|---|---|---|
| SR-D.001 | Durable persistence of all configured state | UR-037 | D | `KryptonianDbContext.cs`, `src/RTSec.Kryptonian.Infrastructure/Repositories/` C; restart on populated SQLite V (tracker) | VT-049 | — | ORK-006, ORK-012 | Existing |
| SR-D.002 | SQLite and PostgreSQL persistence support | UR-037; TBD-006 | D | `KryptonianDbContext.cs` (Npgsql/SQLite) C; docs/DEPLOYMENT.md D; PostgreSQL not exercised D (tracker) | VT-052 | — | ORK-006, ORK-012 | Partial |
| SR-D.003 | Additive schema upgrades, never recreate data | — (`SchemaUpgrades.cs` header rule; PR 41) | D | `src/RTSec.Kryptonian.Infrastructure/Data/SchemaUpgrades.cs` C | VT-051 | — | ORK-011 | Existing |
| SR-D.004 | Upgrade safety boundary | — (KSPEC-010 gap input; docs/formal-specification/07-OPERATIONAL-RISK.md §backup) | D | `SchemaUpgrades.cs` (upgrade DDL outside explicit transaction/backup boundary) C | — | — | ORK-011, ORK-012 | Missing |
| SR-D.005 | UTC timestamp discipline | — (tracker SQLite DateTime-kind fix) | D | tracker (UTC regression fixed with runnable regressions) V; `SchemaUpgrades.cs` C | VT-050 | — | ORK-009 | Existing |
| SR-D.006 | Lifecycle record retention (TBD period) | UR-011 | D | `EnrollmentEventRepository.cs` C; docs/formal-specification/07-OPERATIONAL-RISK.md D | VT-053 | — | ORK-006, ORK-012 | Partial |
| SR-D.007 | Data Protection key persistence | — (`Program.cs` sandbox key ring; docs/formal-specification/07-OPERATIONAL-RISK.md restore order) | D | `Program.cs` (PersistKeysToFileSystem) C; `DataProtectionService.cs` C | — | THR-016 | ORK-012, ORK-015 | Partial |
| SR-D.008 | Sensitive field encryption at rest | UR-013 (**Missing** in KSPEC-002) | D | `DataProtectionService.cs` C (protection service exists; field coverage not evidenced); tracker D | — | THR-016 | ORK-015 | Missing |
| SR-D.009 | Demo operations distinguishable | — (tracker synthetic demo records; demo-enroll endpoint) | D | `EstController.cs` (demo-enroll endpoint) C; tracker D | — | — | — | Partial |

### 4.12 Performance requirements (SR-P)

| SR ID | Short title | UR | Component | Implementation evidence | VT | THR | ORK | Status |
|---|---|---|---|---|---|---|---|---|
| SR-P.001 | EST rate-limit capacity | UR-014; docs/ARCHITECTURE.md | P | `Program.cs` (rate-limiting configuration) C; docs/ARCHITECTURE.md D | VT-055 | THR-013 | — | Partial |
| SR-P.002 | Enrollment request size bounds | — (`EstController.MaxCsrBodySize`; KSPEC-005 ICD-EST) | P | `EstController.cs` (64 KiB cap, 413 mapping) C | VT-003, VT-044 | THR-004, THR-013 | — | Existing |
| SR-P.003 | Enrollment latency targets (TBD) | UR-021 (no baseline performance budget found) | P | no baseline budget found; UR-021 automation rationale D | — | — | — | Missing |
| SR-P.004 | Database connection pooling | — (docs/ARCHITECTURE.md Npgsql pooling) | P | docs/ARCHITECTURE.md D; `DevicesController.cs` AllCertificates N+1 note C | — | — | — | Existing |
| SR-P.005 | Profile/CA certificate caching | — (docs/ARCHITECTURE.md Certificate Caching) | P | docs/ARCHITECTURE.md D; `EnrollmentOrchestrator.cs` C | — | — | — | Partial |

### 4.13 Reliability requirements (SR-R)

| SR ID | Short title | UR | Component | Implementation evidence | VT | THR | ORK | Status |
|---|---|---|---|---|---|---|---|---|
| SR-R.001 | Background watcher fault tolerance | UR-012; `CertificateExpiryWatcher` design | R | `src/RTSec.Kryptonian.Api/HostedServices/CertificateExpiryWatcher.cs` (catches scan exceptions and continues) C | VT-029 | THR-011 | ORK-007, ORK-014 | Existing |
| SR-R.002 | Backend failure outcomes recorded | UR-023; PR 46 (transactional design) | R | `CertificateRevocationService.cs` / enrollment-event persistence C | VT-045 | — | ORK-003 | Existing |
| SR-R.003 | Notification delivery decoupled from lifecycle state | UR-012; `NotificationDispatcher` design | R | `src/RTSec.Kryptonian.Application/Notifications/NotificationDispatcher.cs` C | VT-029 | THR-011 | ORK-014 | Partial |
| SR-R.004 | Concurrency conflict handling | — (PR 46 concurrent-update protection) | R | `CertificateRevocationService.cs`, `UnitOfWork.cs` C; `tests/RTSec.Kryptonian.Infrastructure.Tests/Data/DeviceActivationConcurrencyTests.cs` C | VT-026, VT-048, VT-060 | THR-010 | ORK-005, ORK-008 | Existing |
| SR-R.005 | Graceful shutdown | UR-037 | R | `CertificateExpiryWatcher.cs` (cancellation handling) C | VT-054 | — | — | Existing |

### 4.14 Availability requirements (SR-V)

| SR ID | Short title | UR | Component | Implementation evidence | VT | THR | ORK | Status |
|---|---|---|---|---|---|---|---|---|
| SR-V.001 | Health-check endpoint availability | UR-029; docs/DEPLOYMENT.md liveness probes | V | `src/RTSec.Kryptonian.Api/Controllers/StatusController.cs` C; docs/DEPLOYMENT.md D | VT-056 | — | ORK-010, ORK-013 | Existing |
| SR-V.002 | Component-level health reporting | UR-029; docs/ARCHITECTURE.md health-check example | V | `Program.cs` (AddDbContextCheck) C; `StatusController.cs` C; connector components absent C | VT-056 | — | ORK-010, ORK-013 | Missing |
| SR-V.003 | Restart state preservation | UR-037; tracker | V | restart on populated SQLite preserved profile, issuer, devices V (tracker) | VT-049 | — | ORK-006, ORK-012 | Existing |
| SR-V.004 | Availability targets (TBD) | — (docs/formal-specification/07-OPERATIONAL-RISK.md unavailability scenario) | V | no baseline target exists D | — | — | ORK-013 | Missing |

### 4.15 Audit requirements (SR-U)

| SR ID | Short title | UR | Component | Implementation evidence | VT | THR | ORK | Status |
|---|---|---|---|---|---|---|---|---|
| SR-U.001 | Audit record for every trust decision | UR-011 (High); Mvp.md §8 ("audit everything") | U | `EnrollmentEventRepository.cs` C; `EnrollmentOrchestrator.cs` C | VT-059 | THR-015 | — | Partial |
| SR-U.002 | Enrollment event completeness | UR-009 (blocked enrollment event with reason) | U | `EnrollmentEventRepository.cs` C; docs/flow.md D | VT-059 | THR-015 | ORK-008 | Partial |
| SR-U.003 | Structured, correlated logging | — (Serilog configuration; `RequestIdMiddleware`) | U | `src/RTSec.Kryptonian.Api/Program.cs` (Serilog, correlation/client-IP enrichers) C; docs/ARCHITECTURE.md (Observability) D | — | THR-015 | — | Existing |
| SR-U.004 | Administrative action logging | UR-011 | U | log records with warnings on failures C; systematic admin-action audit records not evidenced UNKNOWN | — | THR-015 | — | Partial |
| SR-U.005 | Audit record tamper protection | — (THR-015 model input; UR-011 rationale) | U | `EnrollmentEventRepository.cs` (no append-only mechanism) C; no tamper-protection evidence UNKNOWN | — | THR-015 | — | Missing |
| SR-U.006 | Audit export capability | — (KSPEC-004 A-6) | U | no export endpoint found in controllers C; docs/formal-specification/04-SOFTWARE-DESIGN.md A-6 D | — | THR-015 | — | Missing |
| SR-U.007 | Live log ring buffer bounds | — (`LiveLogBroadcaster` design) | U | `Program.cs` (LiveLogSink/LiveLogBroadcaster singleton) C; docs/ARCHITECTURE.md D | — | THR-015 | — | Existing |

### 4.16 Deployment requirements (SR-B)

| SR ID | Short title | UR | Component | Implementation evidence | VT | THR | ORK | Status |
|---|---|---|---|---|---|---|---|---|
| SR-B.001 | Deployment packaging options | UR-028 | B | `docker-compose.yml`, `deploy/azure/`, `installer/windows/` C; docs/DEPLOYMENT.md, docs/windows-installation.md D | VT-056 | — | ORK-010, ORK-013 | Existing |
| SR-B.002 | Windows service and sandbox modes | — (`SandboxBootstrap` Production requirement; docs/windows-sandbox.md) | B | `src/RTSec.Kryptonian.Api/Program.cs` (sandbox init; UseWindowsService) C; `src/RTSec.Kryptonian.Api/SandboxBootstrap.cs` C | VT-057 | — | ORK-010, ORK-015 | Existing |
| SR-B.003 | External TLS certificate provisioning | — (`SelfSignedCaGenerator.cs`; docs/ca-backend-configuration.md) | B | `src/RTSec.Kryptonian.Api/SelfSignedCaGenerator.cs` C; `Program.cs` (Kestrel HTTPS, sandbox PFX loading) C; docs/DEPLOYMENT.md D | VT-056 | — | ORK-010 | Existing |
| SR-B.004 | Documented configuration settings | — (KSPEC-005 ICD-CFG; docs/DEPLOYMENT.md) | B | `src/RTSec.Kryptonian.Api/appsettings.json` C; docs/formal-specification/05-INTERFACE-CONTROL.md (configuration tables) D | VT-057 | — | ORK-010 | Existing |
| SR-B.005 | CRL-over-HTTP listener restriction | — (`Program.cs` CrlOnlyHttp middleware; KSPEC-005 ICD-CFG) | B | `Program.cs` (CrlOnlyHttp middleware) C | VT-010 | THR-013 | — | Existing |
| SR-B.006 | Upgrade preservation | — (docs/formal-specification/07-OPERATIONAL-RISK.md MSI upgrade; artifacts/windows-installer/payload/docs/windows-installation.md) | B | `installer/windows/` C; artifacts/windows-installer/payload/docs/windows-installation.md D; docs/formal-specification/07-OPERATIONAL-RISK.md D | — | — | ORK-011 | Existing |
| SR-B.007 | Production secret provisioning | UR-013; KSPEC-004 P-1 | B | docs/DEPLOYMENT.md D; `src/RTSec.Kryptonian.Api/appsettings.Development.json` C; docs/formal-specification/04-SOFTWARE-DESIGN.md P-1 D | VT-057 | THR-016 | ORK-015 | Partial |

### 4.17 Operational maintenance requirements (SR-O)

| SR ID | Short title | UR | Component | Implementation evidence | VT | THR | ORK | Status |
|---|---|---|---|---|---|---|---|---|
| SR-O.001 | Extensible connector architecture | UR-038 | O | `src/RTSec.Kryptonian.Infrastructure/Crypto/CaConnectorFactory.cs`, `src/RTSec.Kryptonian.Domain/Interfaces/` (ICaConnector) C; docs/ARCHITECTURE.md D; new connector not exercised C | — | — | — | Existing |
| SR-O.002 | Log rotation and retention | — (Serilog rolling file sink; docs/DEPLOYMENT.md) | O | `Program.cs` (RollingInterval.Day) C; log retention settings not evidenced UNKNOWN | — | — | — | Partial |
| SR-O.003 | External credential renewal support | — (PR 48; docs/development.md) | O | fresh handshakes observing atomic PFX replacement; expired replacement fails closed V (tracker); docs/development.md D | VT-023 | THR-006 | ORK-001, ORK-015 | Existing |
| SR-O.004 | Metrics endpoint | — (docs/ARCHITECTURE.md Metrics (Future)) | O | no metrics endpoint in controllers C; docs/ARCHITECTURE.md D | — | — | — | Missing |
| SR-O.005 | Dependency maintenance process | — (PR 52) | O | `.github/workflows/` C; tracker (PR 52; MailKit 4.16.0; remaining advisories) D | VT-058 | THR-014 | ORK-016 | Existing |

### 4.18 Failure and recovery requirements (SR-X)

| SR ID | Short title | UR | Component | Implementation evidence | VT | THR | ORK | Status |
|---|---|---|---|---|---|---|---|---|
| SR-X.001 | Unsupported revocation fails closed | UR-010 (§16.4); tracker | X | `CertificateRevocationService.cs` (501 on unsupported CRL generation; state unchanged) C; docs/formal-specification/07-OPERATIONAL-RISK.md D | VT-041 | THR-008 | ORK-008 | Existing |
| SR-X.002 | Invalid replacement credentials fail closed | — (PR 48; docs/development.md) | X | `src/Kryptonian.DICOMTls/` C; expired replacement fails closed V (tracker) | VT-046 | THR-008, THR-012 | ORK-015 | Existing |
| SR-X.003 | Issuer-mismatch and rotated-CA guards (409) | — (docs/formal-specification/07-OPERATIONAL-RISK.md guards CODE-OBSERVED) | X | `CertificateRevocationService.cs` (issuer guards) C; docs/formal-specification/07-OPERATIONAL-RISK.md D | VT-026 | THR-010 | ORK-008 | Existing |
| SR-X.004 | Unencrypted non-CRL requests refused | — (`Program.cs` CrlOnlyHttp; tracker VERIFIED) | X | `Program.cs` (403 for non-CRL on HTTP) C; HTTP listener denial V (tracker) | — | THR-013 | — | Existing |
| SR-X.005 | Activation replay rejected | — (tracker; PR 42 atomic one-time activation) | X | `artifacts/developer/verify-*/replay.txt` V; `EstController.cs` C | VT-015 | THR-003, THR-006 | ORK-005 | Existing |
| SR-X.006 | Database unavailability behavior | — (docs/formal-specification/07-OPERATIONAL-RISK.md outage scenarios) | X | `Program.cs` (AddDbContextCheck) C; `EstController.cs` (500 mapping with logging) C | — | — | ORK-006 | Existing |
| SR-X.007 | Backup and restore procedure | — (docs/formal-specification/07-OPERATIONAL-RISK.md restore sequence) | X | docs/formal-specification/07-OPERATIONAL-RISK.md (restore steps 1–4) D; no automated backup mechanism in code C | VT-053 | — | ORK-006, ORK-012 | Partial |

## 5. UR → SR coverage matrix

SR chains are exactly the KSPEC-003 `Source` edges; VT columns are derived via the SR→VT
edges of §4. Stakeholder group per KSPEC-002 §2.

| UR | Stakeholder group | Covered by SRs (KSPEC-003 Source edges) | VTs (via SRs) | UR status (KSPEC-002) | Coverage |
|---|---|---|---|---|---|
| UR-001 | Hospital IT admins | SR-F-REG.001, SR-F-REG.006, SR-F-CLM.001, SR-F-ADM.001 | VT-014, VT-017, VT-021 (SR-F-ADM.001 untested) | Existing | Partial |
| UR-002 | Hospital IT admins | SR-F-REG.002, SR-F-EST.006, SR-F-EST.012, SR-S.001 | VT-015, VT-048, VT-006, VT-010, VT-031 | Existing | Full |
| UR-003 | Hospital IT admins | SR-F-REG.003, SR-F-REG.004, SR-F-REG.009, SR-F-ADM.001 | VT-014, VT-019 (SR-F-ADM.001 untested) | Existing | Partial |
| UR-004 | Hospital IT admins | SR-F-CAI.001, SR-F-ADM.001 | VT-040 (SR-F-ADM.001 untested) | Existing | Partial |
| UR-005 | Hospital IT admins | SR-F-CAI.002, SR-F-ADM.001 | VT-040 (SR-F-ADM.001 untested) | Existing | Partial |
| UR-006 | Hospital IT admins | SR-F-ADM.002, SR-F-ADM.008, SR-F-ADM.009 | — (no VT for any chain SR) | Existing | None |
| UR-007 | Hospital IT admins | SR-F-ADM.003, SR-F-ADM.005 | VT-037 (Partial; setup and last-admin guards require dedicated coverage) | Existing | Partial |
| UR-008 | Hospital cyber personnel | SR-F-REG.008, SR-F-EST.009, SR-F-CLM.001, SR-S.003, SR-S.004 | VT-018, VT-008, VT-021, VT-033, VT-034 | Existing | Full |
| UR-009 | Hospital cyber personnel | SR-F-REG.008, SR-F-CLM.009, SR-Z.005, SR-U.002, SR-C.009 | VT-018, VT-027, VT-038 (SR-U.002, SR-C.009 untested) | Existing | Partial |
| UR-010 | Hospital cyber personnel | SR-F-CLM.007, SR-F-CLM.008, SR-F-CLM.009, SR-F-CAI.006, SR-F-CAI.008, SR-S.012, SR-Z.005, SR-C.005, SR-C.006, SR-C.009, SR-X.001 | VT-025, VT-026, VT-027, VT-028, VT-041, VT-043 (SR-S.012, SR-C.009 untested) | Partial | Partial |
| UR-011 | Hospital cyber personnel | SR-F-REG.010, SR-F-ADM.009, SR-C.010, SR-D.006, SR-U.001, SR-U.004 | VT-014, VT-053, VT-059 (Proposed audit completeness; SR-F-ADM.009 and SR-U.004 lack direct VTs) | Partial | Partial |
| UR-012 | Hospital cyber personnel | SR-F-CLM.005, SR-F-CLM.006, SR-F-ADM.006, SR-R.001, SR-R.003 | VT-029 (SR-F-ADM.006 untested; delivery not exercised) | Partial | Partial |
| UR-013 | Hospital cyber personnel | SR-F-CAI.010, SR-S.007, SR-S.008, SR-A.006, SR-D.008, SR-B.007 | VT-035, VT-057 (only SR-B.007 chain is Partial; rest Missing) | **Missing** | Partial |
| UR-014 | Hospital cyber personnel | SR-S.001, SR-S.006, SR-P.001 | VT-031, VT-055 | Existing | Partial |
| UR-015 | Hospital cyber personnel | SR-F-ADM.001, SR-F-ADM.003, SR-F-ADM.005, SR-S.009, SR-S.013, SR-A.001, SR-A.009, SR-Z.001, SR-Z.002, SR-Z.003, SR-Z.004 | VT-037, VT-038, VT-039, VT-031, VT-036 (SR-F-ADM.001, SR-F-ADM.005, SR-A.009 untested) | Existing | Partial |
| UR-016 | Device manufacturers | SR-F-EST.001, SR-F-EST.003, SR-F-EST.004 | VT-001, VT-003, VT-004, VT-044 | Existing | Full |
| UR-017 | Device manufacturers | SR-F-EST.002 | VT-002 | Existing | Full |
| UR-018 | Device manufacturers | SR-F-EST.004, SR-F-CAI.004 | VT-004, VT-040, VT-045 | Existing | Full |
| UR-019 | Device manufacturers | SR-F-EST.006, SR-F-EST.007, SR-F-INT.001, SR-F-INT.002 | VT-006, VT-011 | Partial | Partial |

| UR-020 | Device software developers | SR-F-REG.005, SR-F-REG.009 | VT-016, VT-014 | Existing | Full |
| UR-021 | Device software developers | SR-F-EST.008, SR-F-EST.009, SR-F-CLM.002, SR-F-CLM.003, SR-C.004, SR-P.003 | VT-007, VT-008, VT-022, VT-023, VT-024 (SR-P.003 is **Missing** — no latency budget) | Existing | Partial |
| UR-022 | Device software developers | SR-F-EST.001, SR-F-EST.005 | VT-001, VT-005 | Existing | Full |
| UR-023 | Device software developers | SR-F-REG.005, SR-F-EST.003, SR-F-EST.010, SR-F-EST.011, SR-F-EST.013, SR-A.007, SR-R.002 | VT-016, VT-003, VT-009, VT-012, VT-044, VT-006, VT-045 (SR-F-EST.011 **Missing**) | Existing | Partial |
| UR-024 | Clinical engineering | SR-F-CLM.004, SR-F-CLM.005, SR-F-ADM.001, SR-F-ADM.008 | VT-029 (SR-F-ADM.001, SR-F-ADM.008 untested) | Existing | Partial |
| UR-025 | Clinical engineering | SR-F-CLM.002 | VT-022 | Existing | Full |
| UR-026 | Clinical engineering | SR-F-INT.006, SR-Z.010 | VT-038 (SR-F-INT.006 is documentation-boundary, inspection-only) | Existing | Partial |
| UR-027 | Healthcare system integrators | SR-F-INT.003, SR-F-ADM.004, SR-A.001, SR-A.003, SR-Z.006 | VT-039, VT-037 (SR-F-INT.003 **Missing** — empty/stale OpenAPI) | Existing | Partial |
| UR-028 | Healthcare system integrators | SR-B.001 | VT-056 (Proposed — no container deployment run recorded) | Existing | Partial |
| UR-029 | Healthcare system integrators | SR-F-ADM.010, SR-V.001, SR-V.002 | VT-056 (SR-V.002 **Missing**) | Existing | Partial |
| UR-030 | Enterprise PKI admins | SR-F-CAI.003, SR-F-CAI.005, SR-F-CAI.006, SR-F-CAI.007 | VT-043, VT-042, VT-040, VT-045 (hospital ADCS/EJBCA interop unverified) | Existing | Partial |
| UR-031 | Enterprise PKI admins | SR-F-ADM.007, SR-F-INT.004, SR-C.001, SR-C.003 | VT-030, VT-033 (SR-F-ADM.007, SR-F-INT.004 untested) | Existing | Partial |
| UR-032 | Enterprise PKI admins | SR-F-ADM.007, SR-C.002 | VT-033 (SR-F-ADM.007 untested) | Existing | Partial |
| UR-033 | Enterprise PKI admins | SR-S.002 | VT-032 | Existing | Full |
| UR-034 | OR.NET research community | SR-F-INT.005, SR-F-INT.007 | VT-047, VT-011 | Existing | Full |
| UR-035 | OR.NET research community | SR-F-INT.006 | — (documentation-boundary, inspection-only) | Existing | Partial |
| UR-036 | OR.NET research community | SR-F-INT.008 | VT-058 (Proposed — joint OR.NET vectors excluded) | Partial | Partial |
| UR-037 | Cross-cutting | SR-F-CAI.009, SR-D.001, SR-D.002, SR-R.005, SR-V.003 | VT-049, VT-052, VT-051, VT-054 (PostgreSQL unverified) | Partial | Partial |
| UR-038 | Cross-cutting | SR-F-CAI.003, SR-O.001 | VT-043, VT-040 (SR-O.001 untested — new connector not exercised) | Existing | Partial |

### 5.1 Coverage summary

| Metric | Value |
|---|---|
| Stakeholder needs (UR) | 38 (UR-001 – UR-038) |
| URs traced to at least one SR | 38 of 38 (UR-007 bound to SR-F-ADM.003 and SR-F-ADM.005) |
| URs with at least one linked VT | 35 of 38 (untested: UR-006, UR-035; UR-028's only VT, VT-056, is Proposed) |
| URs fully covered (§2.3) | 10 (UR-002, UR-008, UR-016, UR-017, UR-018, UR-020, UR-022, UR-025, UR-033, UR-034) |
| URs partially covered | 26 |
| URs with no coverage | 2 (UR-006 — implemented but untested chain; UR-035 — documentation-boundary) |

## 6. Reverse matrix: VT → linked requirements

Copied from the current KSPEC-008 records. Evidence labels are not execution claims.

| VT ID | Linked requirements | Evidence status |
|---|---|---|
| VT-001 | SR-F-EST.001 | Covered — AS-IS automated test: `tests/RTSec.Kryptonian.Api.Tests/Integration/EstRouteTests.cs` and `EstIntegrationTests.cs` (assertions CODE-OBSERVED; CI execution per tracker PR 40/49). |
| VT-002 | SR-F-EST.002 | Covered — AS-IS automated test: `tests/RTSec.Kryptonian.Api.Tests/Controllers/EstControllerTests.cs` (assertions CODE-OBSERVED). |
| VT-003 | SR-F-EST.003, SR-P.002 | Covered — AS-IS automated test: `EstControllerTests.cs` (limit CODE-OBSERVED); live-boundary oversize case Proposed. |
| VT-004 | SR-F-EST.004 | Covered — AS-IS automated test: `EstControllerTests.cs`, `EstIntegrationTests.cs` (assertions CODE-OBSERVED); live-boundary curl case executed per tracker (see VT-046). |
| VT-005 | SR-F-EST.005 | Covered — AS-IS automated test: `EstRouteTests.cs` (PR 37 regression, CODE-OBSERVED); routing behavior VERIFIED per tracker ("Profile issuer routing and unknown-label rejection", PR 37). |
| VT-006 | SR-F-EST.006, SR-A.007 | Covered — executed local evidence (VERIFIED): tracker records "Independent curl HTTP Basic EST over real HTTPS; no private enrollment headers." and PR 42 (standard Basic EST bootstrap). |
| VT-007 | SR-F-EST.008, SR-A.008 | Covered — executed local evidence (VERIFIED): tracker records "renewal uses a native TLS client certificate, generates a new key and preserves approved identity"; AS-IS tests: `EstEnrollmentClientRenewalTests.cs` (CODE-OBSERVED). |
| VT-008 | SR-F-EST.009 | Covered — AS-IS automated test: `tests/RTSec.Kryptonian.Api.Tests/Authentication/ConfiguredClientCertificateTrustTests.cs` (CODE-OBSERVED); tracker records "Developer trust adds a separate issuer/hostname/purpose regression." |
| VT-009 | SR-F-EST.010 | Covered — AS-IS automated test: `EstControllerTests.cs`; client behavior: `tests/Kryptonian.MedicalDevice.Enrollment.Tests/EstEnrollmentClientPendingTests.cs` (both CODE-OBSERVED). |
| VT-010 | SR-F-EST.012, SR-B.005 | Covered — executed local evidence (VERIFIED): tracker records "HTTP listener denies non-CRL requests"; AS-IS test: `EstRouteTests.cs` (CODE-OBSERVED). |
| VT-011 | SR-F-EST.007, SR-F-INT.002, SR-F-INT.007 | Partially covered — standard-path independence VERIFIED per tracker ("no private enrollment headers"); legacy-header compatibility inspection Proposed (SR-F-EST.007 status Partial). |
| VT-012 | SR-F-EST.013, SR-F-EST.011 (Missing) | Proposed — SR-F-EST.011 is Missing at baseline; SR-F-EST.013 has Inspection-level support via existing error paths (CODE-OBSERVED) but no assertion test exists. |
| VT-013 | SR-F-EST.014 (Missing) | Proposed — requirement Missing at baseline (KSPEC-003 register). |
| VT-014 | SR-F-REG.001, SR-F-REG.009, SR-F-REG.010, SR-F-REG.003 | Covered — AS-IS automated test: `tests/RTSec.Kryptonian.Application.Tests/Services/DeviceServiceActivationTests.cs` (CODE-OBSERVED); admin portal demonstration per tracker's twenty device enrollment/lifecycle tests. |
| VT-015 | SR-F-REG.002, SR-X.005 | Covered — executed local evidence (VERIFIED): tracker records "One-time activation replay rejected". AS-IS tests: `DeviceServiceActivationTests.cs`, `tests/RTSec.Kryptonian.Infrastructure.Tests/Data/DeviceActivationConcurrencyTests.cs` (CODE-OBSERVED). |
| VT-016 | SR-F-REG.005 | Covered — executed local evidence (VERIFIED): tracker records PR 38 "Authenticated enrollment identity binding"; AS-IS test: `DeviceServiceActivationTests.cs` (CODE-OBSERVED). |
| VT-017 | SR-F-REG.006 | Covered — AS-IS automated test: `DeviceServiceActivationTests.cs` (pending-state assertions CODE-OBSERVED). |
| VT-018 | SR-F-REG.008 | Covered — AS-IS automated test: `tests/Kryptonian.MedicalDevice.Enrollment.Tests/GatewayTrustTests.cs` (CODE-OBSERVED). |
| VT-019 | SR-F-REG.004 | Proposed — state-machine traversal test not present as a dedicated AS-IS test; partial coverage via `DeviceServiceActivationTests.cs` (CODE-OBSERVED). |
| VT-020 | SR-F-REG.007 (Missing), SR-Z.008 | Proposed — SR-F-REG.007 is Missing at baseline. |
| VT-021 | SR-F-CLM.001 | Covered — AS-IS automated test: `tests/RTSec.Kryptonian.Application.Tests/Services/EnrollmentOrchestratorTests.cs` (CODE-OBSERVED). |
| VT-022 | SR-F-CLM.002, SR-C.004 | Covered — executed local evidence (VERIFIED): tracker records "Twenty device enrollment/lifecycle tests passed including actual Schannel mTLS with the public client's returned certificate"; renewal evidence "renewal uses a native TLS client certificate". AS-IS tests: `EstEnrollmentClientRenewalTests.cs` (CODE-OBSERVED). |
| VT-023 | SR-F-CLM.003, SR-A.008, SR-O.003 | Covered — executed local evidence (VERIFIED): tracker records renewal "generates a new key and preserves approved identity" and the Schannel key-persistence fix (PR 50) with a runnable regression (`InstalledCertificateRenewalTests.cs`, CODE-OBSERVED). |
| VT-024 | SR-C.004 | Covered — AS-IS automated test: `EstEnrollmentClientRenewalTests.cs` (window logic CODE-OBSERVED). |
| VT-025 | SR-F-CLM.007, SR-C.005, SR-C.006 | Covered — executed local evidence (VERIFIED): tracker records "Administrator revocation followed by native online X509Chain reporting Revoked, independently of registry lookup and without flushing the system CRL cache."; AS-IS tests: `CertificateRevocationServiceTests.cs`, `IssuerCrlStatePersistenceTests.cs`, `PkcsServiceTests.cs` (CODE-OBSERVED). |
| VT-026 | SR-F-CLM.008, SR-R.004, SR-X.003 | Covered — AS-IS automated test: `CertificateRevocationServiceTests.cs` (transactional and conflict paths CODE-OBSERVED); `UnitOfWork.cs` conflict handling CODE-OBSERVED. |
| VT-027 | SR-F-CLM.009 | Covered — AS-IS automated test: `CertificateRevocationServiceTests.cs` (CODE-OBSERVED); revocation-online enforcement at peers VERIFIED per tracker (see VT-028). |
| VT-028 | SR-C.007 | Covered — executed local evidence (VERIFIED): tracker records "Administrator revocation followed by native online X509Chain reporting Revoked, independently of registry lookup and without flushing the system CRL cache." and "Both peers enforce issuer, TLS purpose and online revocation." |
| VT-029 | SR-F-CLM.004, SR-F-CLM.005, SR-F-CLM.006, SR-R.001, SR-R.003 | Partially covered — AS-IS automated test: `NotificationDispatcherTests.cs` (CODE-OBSERVED); watcher fault-tolerance and end-to-end email delivery Proposed (tracker records delivery behavior at baseline as unexercised). |
| VT-030 | SR-C.001, SR-C.003 | Covered — AS-IS automated test: `tests/RTSec.Kryptonian.Application.Tests/Services/EnrollmentCertificatePolicyTests.cs` and `EstProfilePartialUpdateTests.cs` (CODE-OBSERVED). |
| VT-031 | SR-S.001, SR-S.013 | Partially covered — HTTPS-only EST VERIFIED per tracker (see VT-010); CORS assertions Proposed (no dedicated AS-IS test observed). |
| VT-032 | SR-S.002 | Covered — AS-IS automated test: `EnrollmentCertificatePolicyTests.cs` (CODE-OBSERVED); PR 43 ("CSR and returned-certificate policy enforcement") per tracker. |
| VT-033 | SR-S.003, SR-C.002 | Covered — executed local evidence (VERIFIED): tracker records "Gateway restart on populated SQLite preserved configured profile, issuer and devices."; AS-IS tests: `EstProfilePersistenceTests.cs`, `EstProfilePartialUpdateTests.cs` (CODE-OBSERVED). |
| VT-034 | SR-S.004, SR-S.005 (Missing) | Partially covered — executed local evidence (VERIFIED): tracker records "Direct forwarded-certificate spoof rejected"; AS-IS test: `tests/RTSec.Kryptonian.Api.Tests/Middleware/CertificateForwardingGuardTests.cs` (CODE-OBSERVED); SR-S.005 (authenticated forwarder) Missing → that portion Proposed. |
| VT-035 | SR-S.007 (Missing), SR-A.006 (Missing) | Proposed — SR-S.007/SR-A.006 Missing at baseline (hard-coded dev fallback recorded as a weak control in KSPEC-003 §22). |
| VT-036 | SR-S.009 | Proposed — Inspection-method requirement; no automated test applicable. (Code path CODE-OBSERVED in `UsersController.cs`/auth services per KSPEC-003 evidence.) |
| VT-037 | SR-A.001, SR-A.002, SR-A.010, SR-F-ADM.003 | Partially covered — admin-setup behavior is documented; device lifecycle tests do not establish user-management or JWT coverage (DOCUMENTED only); dedicated token-validation tests Proposed (no dedicated AS-IS test file observed under `tests/RTSec.Kryptonian.Api.Tests/Authentication/`). |
| VT-038 | SR-Z.001, SR-Z.002, SR-Z.003, SR-Z.004, SR-Z.005, SR-Z.010 | Covered — AS-IS automated tests: `tests/RTSec.Kryptonian.Api.Tests/Controllers/DevicesControllerAuthorizationTests.cs`, `CertificateRevocationControllerAuthorizationTests.cs` (CODE-OBSERVED). |
| VT-039 | SR-A.003, SR-A.004, SR-A.005, SR-Z.006, SR-F-ADM.004, UR-027 | Proposed — behavior CODE-OBSERVED in `ApiKeyAuthenticationHandler.cs` per KSPEC-003 evidence, but no dedicated AS-IS test file observed; assertion set Proposed. |
| VT-040 | SR-F-CAI.001, SR-F-CAI.002, SR-F-CAI.004 | Covered — AS-IS automated test: `tests/RTSec.Kryptonian.Application.Tests/Services/CaBackendServiceTests.cs` (CODE-OBSERVED). |
| VT-041 | SR-F-CAI.008, SR-F-CAI.009, SR-X.001 | Covered — AS-IS automated tests: `tests/RTSec.Kryptonian.Infrastructure.Tests/Crypto/SelfSignedCaConnectorTests.cs`, `CertificateRevocationServiceTests.cs` (fail-closed path CODE-OBSERVED); live revocation per tracker (VT-025). |
| VT-042 | SR-F-CAI.007 | Covered — AS-IS automated tests: `tests/RTSec.Kryptonian.Infrastructure.Tests/Acme/AcmeCaConnectorTests.cs`, `Http01ChallengeStoreTests.cs`, `AcmeConnectorConfigTests.cs`, `WellKnownServersTests.cs` (CODE-OBSERVED); dns-01 remains Partial (SR-F-CAI.007). |
| VT-043 | SR-F-CAI.005, SR-F-CAI.006, SR-F-CAI.003 | Proposed — tracker records "actual ADCS/EJBCA hospital enrollment/revocation interoperability remains unverified"; connector code CODE-OBSERVED (`AdcsCaConnector.cs`, `EjbcaCaConnector.cs`, `ScepClient.cs`). |
| VT-044 | SR-F-EST.003, SR-F-EST.004, SR-F-EST.013, SR-P.002 | Partially covered — `EstControllerTests.cs`/`EstRouteTests.cs` cover core rejections (CODE-OBSERVED); the full mutation matrix Proposed. |
| VT-045 | SR-R.002, SR-F-CAI.004 | Covered — AS-IS automated test: `EnrollmentOrchestratorTests.cs` (error paths CODE-OBSERVED). |
| VT-046 | SR-X.002 | Covered — executed local evidence (VERIFIED): tracker records "Five loopback proxy tests passed, including unknown/expired/revoked client rejection and fresh handshakes observing atomic PFX replacement; expired replacement fails closed."; AS-IS tests: `DimseTlsProxyWorkerTests.cs`, `ServerCertificateRotationTests.cs` (CODE-OBSERVED). |
| VT-047 | SR-F-INT.005, SR-C.007 | Covered — executed local evidence (VERIFIED): tracker records "Two actual mutual-TLS DICOM C-STORE transfers followed by received-file reopen and SOP Instance UID comparison. Both peers enforce issuer, TLS purpose and online revocation." with SOP UIDs `2.25.154518358314734480382348393848679450333` and `2.25.281306672341327719638925984637794989584`; parser unit test `tests/RTSec.Kryptonian.Bridge.Tests/DicomWebMultipartParserTests.cs`. |
| VT-048 | SR-F-REG.002, SR-R.004 | Covered — AS-IS automated test: `tests/RTSec.Kryptonian.Infrastructure.Tests/Data/DeviceActivationConcurrencyTests.cs` (CODE-OBSERVED); replay rejection VERIFIED per tracker (VT-015). |
| VT-049 | SR-D.001, SR-V.003 | Covered — executed local evidence (VERIFIED): tracker records "Gateway restart on populated SQLite preserved configured profile, issuer and devices." |
| VT-050 | SR-D.005 | Covered — executed local evidence (VERIFIED): tracker records "SQLite's unspecified DateTime kind shifted refreshed CRL revocation times five hours into the future on this host ... fixed with runnable regressions." |
| VT-051 | SR-D.003 | Covered — AS-IS automated test: `tests/RTSec.Kryptonian.Infrastructure.Tests/Data/SchemaUpgradesTests.cs` (CODE-OBSERVED). |
| VT-052 | SR-D.002, UR-037 | Proposed — tracker records "PostgreSQL schema upgrades are implemented but only SQLite persistence was exercised." |
| VT-053 | SR-X.007 (Partial), SR-D.006 | Proposed — no restore drill recorded at baseline. |
| VT-054 | SR-R.005 | Partially covered — cancellation handling CODE-OBSERVED in `CertificateExpiryWatcher.cs` (Inspection-level, SR-R.005 method I); mid-enrollment stop test Proposed. |
| VT-055 | SR-S.006, SR-P.001, UR-014 | Proposed — tracker/UR-014 note rate limiting as configuration-documented with effectiveness not separately evidenced; production limits TBD (SR-S.006). |
| VT-056 | SR-B.001, SR-B.003, SR-V.001, SR-V.002 (Missing), SR-F-ADM.010, UR-028, UR-029 | Proposed — no container deployment run recorded in the tracker (Windows/SQLite developer path is the recorded evidence). |
| VT-057 | SR-B.002, SR-B.004, SR-B.007 (Partial) | Covered — AS-IS automated test: `tests/RTSec.Kryptonian.Api.Tests/SandboxBootstrapTests.cs` (CODE-OBSERVED); sandbox mode exercised by the developer verifier evidence path per tracker. |
| VT-058 | SR-O.005, SR-S.014, SR-F-INT.008 (Proposed joint vectors excluded) | Covered — executed local evidence (VERIFIED): tracker records "Windows GitHub CI builds the UI, tests the solution and builds the standalone proxy." and the npm-audit reduction "from six vulnerable packages to zero; CI now rejects high/critical UI dependency advisories", with the two remaining high advisories listed as deployment blockers. Eighteen pre-existing Windows infrastructure skips acknowledged. |
| VT-059 | SR-C.010, SR-U.001, SR-U.002 | Proposed — this review observed missing branches; this acceptance test has not been implemented or executed. |
| VT-060 | SR-F-REG.002, SR-R.004 | Proposed — consumption order is CODE-OBSERVED; failure/crash/pending recovery execution is absent. |

## 7. Security threat (THR) and operational risk (ORK) linkage

### 7.1 THR-001 – THR-018 (KSPEC-006 §7/§8 → SR/VT)

Specific SR/VT bindings combine the exact IDs recorded in KSPEC-008 `Linked THR/ORK`
fields with the KSPEC-006 §8 prefix intent. Band per KSPEC-006 §9.3.

| THR | STRIDE (primary) | Band | Linked SR prefixes (KSPEC-006 §8) | Specific SRs (resolved via VTs) | Linked VTs |
|---|---|---|---|---|---|
| THR-001 | S | Medium–High | SR-A, SR-Z, SR-C | SR-F-EST.009, SR-F-REG.005, SR-F-INT.005, SR-C.007 | VT-008, VT-016, VT-047 |
| THR-002 | E | High | SR-Z, SR-S, SR-C | SR-F-EST.001, SR-F-REG.006, SR-F-CLM.001 | VT-001, VT-017, VT-021 |
| THR-003 | S, R | High | SR-A, SR-S, SR-C | SR-F-EST.006, SR-F-EST.012, SR-F-REG.001/.002/.003/.009/.010, SR-A.007, SR-S.001, SR-S.013, SR-X.005, SR-R.004 | VT-006, VT-010, VT-014, VT-015, VT-031, VT-048 |
| THR-004 | T | Medium–High | SR-C, SR-S | SR-F-EST.003, SR-F-EST.013, SR-F-EST.014, SR-P.002, SR-S.002, SR-C.001, SR-C.003 | VT-003, VT-013, VT-030, VT-032, VT-044 |
| THR-005 | T, E | Medium–High | SR-C, SR-S, SR-F | SR-F-EST.004, SR-F-CAI.003/.005/.006/.007, SR-S.002, SR-C.001, SR-C.003 | VT-004, VT-030, VT-032, VT-042, VT-043 |
| THR-006 | R | Low–Medium | SR-A, SR-C | SR-F-EST.008, SR-F-REG.002, SR-A.008, SR-F-CLM.003, SR-O.003, SR-X.005 | VT-007, VT-015, VT-023, VT-048 |
| THR-007 | E, S, I | High | SR-A, SR-Z, SR-S, SR-U | SR-A.001–.005, SR-A.010, SR-Z.001–.007, SR-F-ADM.003, SR-F-ADM.004, SR-S.007 | VT-035, VT-037, VT-038, VT-039 |
| THR-008 | E, T, S | High | SR-C, SR-S, SR-X | SR-F-CAI.008, SR-F-CAI.009, SR-X.001, SR-X.002 | VT-041, VT-043, VT-046 |
| THR-009 | S, I, E | High | SR-A, SR-S, SR-U | SR-A.001, SR-A.002, SR-A.006, SR-A.010, SR-S.007, SR-S.009, SR-F-ADM.005 | VT-035, VT-036, VT-037 |
| THR-010 | D, T, Rp | High | SR-C, SR-V, SR-U | SR-F-CLM.007/.008/.009, SR-C.005/.006/.007/.008/.009, SR-R.004, SR-X.003 | VT-025, VT-026, VT-027, VT-028 |
| THR-011 | D | High | SR-V, SR-C, SR-O | SR-F-CLM.002/.003/.004/.005/.006, SR-C.004, SR-R.001, SR-R.003 | VT-022, VT-023, VT-024, VT-029 |
| THR-012 | E, S | Medium–High | SR-S, SR-Z, SR-C | SR-F-EST.002/.005, SR-F-REG.008, SR-S.003, SR-C.002, SR-X.002 | VT-002, VT-005, VT-018, VT-033, VT-046 |
| THR-013 | D | Medium | SR-V, SR-P, SR-R | SR-F-EST.003/.012, SR-F-REG.007, SR-Z.008, SR-S.006, SR-P.001, SR-P.002, SR-B.005 | VT-003, VT-010, VT-020, VT-044, VT-055 |
| THR-014 | T, I | Critical (deployment blockers) | SR-B, SR-S, SR-O | SR-S.014, SR-O.005 | VT-058 |
| THR-015 | Rp, T | High | SR-U, SR-D, SR-Z | SR-U.001–.007, SR-D.006 | VT-059 (Proposed) |
| THR-016 | I, E | High | SR-S, SR-D, SR-B | SR-S.001, SR-S.007, SR-S.008, SR-S.010, SR-S.011, SR-S.013, SR-A.006, SR-D.007, SR-D.008, SR-B.007 | VT-031, VT-035 |
| THR-017 | S, T | Medium–High | SR-A, SR-Z, SR-U | SR-S.004, SR-S.005 | VT-034 |
| THR-018 | I | Low–Medium | SR-S, SR-U, SR-D | SR-U.003, SR-U.007, SR-S.013 | **— (no VT)** |

### 7.2 ORK-001 – ORK-016 (KSPEC-007 §3/§7.1 → SR/VT)

Target-area intent is KSPEC-007 §7.1; specific SR/VT bindings come from KSPEC-008 VT
records. Band per KSPEC-007 §4 FMEA summary. Revision 0.2 resolves the prior
unallocated interoperability prefix; current references use SR-F-INT or SR-F-CAI
as appropriate.

| ORK | Failure mode | Band | Target areas (KSPEC-007 §7.1) | Specific SRs (resolved via VTs) | Linked VTs |
|---|---|---|---|---|---|
| ORK-001 | Certificate renewal failure | High | SR-C, SR-R, SR-X | SR-F-CLM.002/.003, SR-C.004, SR-F-CLM.004/.005/.006, SR-R.001, SR-R.003, SR-O.003 | VT-022, VT-023, VT-024, VT-029 |
| ORK-002 | Device communication interruption | High | SR-V, SR-F-INT, SR-R | SR-F-INT.005, SR-C.007 | VT-047 |
| ORK-003 | CA unavailability | Medium | SR-C, SR-F-CAI, SR-X | SR-F-CAI.001/.002/.003/.004/.005/.006, SR-R.002 | VT-040, VT-043, VT-045 |
| ORK-004 | Incorrect certificate policy configuration | High | SR-A, SR-Z, SR-S, SR-C | SR-F-CLM.001, SR-C.001, SR-C.003 | VT-021, VT-030 |
| ORK-005 | Unauthorized trust relationship | High¹ | SR-A, SR-Z, SR-S, SR-C | SR-F-EST.006, SR-F-REG.002/.008, SR-S.003, SR-A.007, SR-X.005, SR-R.004 | VT-006, VT-015, VT-018, VT-033, VT-048 |
| ORK-006 | Database corruption | Critical | SR-D, SR-X, SR-O | SR-D.001/.002/.006, SR-V.003, SR-X.007 | VT-049, VT-052, VT-053 |
| ORK-007 | Certificate expiration (unmanaged) | High | SR-C, SR-R, SR-X | SR-F-CLM.004/.005/.006, SR-R.001, SR-R.003 | VT-029 |
| ORK-008 | Failed revocation | Critical | SR-C, SR-F-CAI, SR-X | SR-F-EST.009, SR-F-CLM.007/.008, SR-C.005/.006/.007/.009, SR-F-CAI.006/.008, SR-R.004, SR-X.003 | VT-008, VT-025, VT-026, VT-028, VT-043 |
| ORK-009 | Clock synchronization errors | Critical¹ | SR-R, SR-C, SR-D | SR-D.005, SR-S.012 | VT-050 |
| ORK-010 | Configuration errors | High | SR-B, SR-S, SR-O, SR-X | SR-F-CAI.001/.002, SR-F-ADM.008/.010, SR-B.001/.002/.004, SR-V.001 | VT-040, VT-056, VT-057 |
| ORK-011 | Software upgrade failure | Critical | SR-B, SR-S, SR-O, SR-X | SR-D.003, SR-B.006 | VT-051 |
| ORK-012 | Backup and recovery failure | Critical¹ | SR-D, SR-X, SR-O | SR-D.001/.002/.006/.007, SR-V.003, SR-X.007 | VT-049, VT-052, VT-053 |
| ORK-013 | Network partitioning | High | SR-V, SR-F-INT, SR-R | SR-B.001, SR-F-ADM.010, SR-V.001, SR-V.002 | VT-056 |
| ORK-014 | Notification delivery failure | Critical¹ | SR-O, SR-R, SR-U | SR-F-ADM.006, SR-F-CLM.005/.006, SR-R.001, SR-R.003 | VT-029 |
| ORK-015 | Secrets / credential rotation failure | Critical¹ | SR-S, SR-A, SR-O, SR-B | SR-F-CLM.003, SR-A.008, SR-O.003, SR-X.002, SR-B.002/.007, SR-S.010 | VT-023, VT-046, VT-057 |
| ORK-016 | Dependency vulnerability exposure | High | SR-S, SR-A, SR-O, SR-B | SR-S.014, SR-O.005 | VT-058 |

¹ Band reached or raised via the KSPEC-007 §2.5 overrides.

## 8. Reverse coverage statistics (SR → VT)

114 of 154 SRs link to at least one specified VT; 40 have no direct VT link. A proposed VT is a plan, not executed evidence.

| Area | SRs | VT-linked | Not VT-linked |
|---|---|---|---|
| SR-F-REG | 10 | 10 | 0 |
| SR-F-EST | 14 | 14 | 0 |
| SR-F-CLM | 9 | 9 | 0 |
| SR-F-CAI | 10 | 9 | 1 |
| SR-F-ADM | 10 | 3 | 7 |
| SR-F-INT | 8 | 4 | 4 |
| SR-S | 14 | 10 | 4 |
| SR-A | 10 | 9 | 1 |
| SR-Z | 10 | 8 | 2 |
| SR-C | 10 | 8 | 2 |
| SR-D | 9 | 5 | 4 |
| SR-P | 5 | 2 | 3 |
| SR-R | 5 | 5 | 0 |
| SR-V | 4 | 3 | 1 |
| SR-U | 7 | 2 | 5 |
| SR-B | 7 | 6 | 1 |
| SR-O | 5 | 2 | 3 |
| SR-X | 7 | 5 | 2 |
| Total | 154 | 114 | 40 |

## 9. Gap lists

These lists feed KSPEC-010 (GAP-*) and are stated here as traceability findings only; no
remediation is planned in this document.

### 9.1 Unimplemented requirements (KSPEC-003 status **Missing**, 18 of 154)

| SR | Short title | KSPEC-002 impact |
|---|---|---|
| SR-F-REG.007 | Anonymous endpoint abuse controls | VT-020 Proposed (TO-BE) |
| SR-F-EST.011 | Deterministic pending semantics per backend | Client error diagnosis (UR-023) incomplete |
| SR-F-EST.014 | CSR attributes (`csrattrs`) endpoint | KSPEC-004 P-3, awaiting approval |
| SR-F-CAI.010 | Secure connector credential handling | UR-013 (Missing) |
| SR-F-INT.003 | Authoritative OpenAPI admin contract | `OpenAPI.Client.yaml` empty; `OpenAPI.Schema.yaml` stale |
| SR-S.005 | Authenticated forwarder identity | XFCC trusted by IP only (P-2) |
| SR-S.007 | No hard-coded signing secrets | Dev fallback constant in `Program.cs` |
| SR-S.008 | Secrets at rest | UR-013 |
| SR-S.012 | Configurable CRL lifetime | 60 s prototype value (A-4) |
| SR-A.006 | JWT signing secret provisioning | Same baseline finding as SR-S.007 |
| SR-D.004 | Upgrade safety boundary | Upgrade DDL outside transaction/backup boundary |
| SR-D.008 | Sensitive field encryption at rest | UR-013 |
| SR-P.003 | Enrollment latency targets (TBD) | No baseline budget; value TBD, not invented |
| SR-V.002 | Component-level health reporting | Health lacks connector components (P-7) |
| SR-V.004 | Availability targets (TBD) | No baseline target |
| SR-U.005 | Audit record tamper protection | THR-015 dominant residual risk |
| SR-U.006 | Audit export capability | KSPEC-004 A-6 |
| SR-O.004 | Metrics endpoint | docs/ARCHITECTURE.md marks as future (P-4) |

### 9.2 Requirements with no direct VT link (40 of 154)

This is a linkage gap, not a statement that the others are completely tested.

- SR-F-CAI.010
- SR-F-ADM.001
- SR-F-ADM.002
- SR-F-ADM.005
- SR-F-ADM.006
- SR-F-ADM.007
- SR-F-ADM.008
- SR-F-ADM.009
- SR-F-INT.001
- SR-F-INT.003
- SR-F-INT.004
- SR-F-INT.006
- SR-S.008
- SR-S.010
- SR-S.011
- SR-S.012
- SR-A.009
- SR-Z.007
- SR-Z.009
- SR-C.008
- SR-C.009
- SR-D.004
- SR-D.007
- SR-D.008
- SR-D.009
- SR-P.003
- SR-P.004
- SR-P.005
- SR-V.004
- SR-U.003
- SR-U.004
- SR-U.005
- SR-U.006
- SR-U.007
- SR-B.006
- SR-O.001
- SR-O.002
- SR-O.004
- SR-X.004
- SR-X.006

### 9.3 Untraceable code behavior (observed behavior with no covering SR, or only Partial coverage)

1. Legacy activation-header fallback path in `EstController.cs` has **no disable switch**
   (SR-F-EST.007 Partial; KSPEC-002 §16.3 boundary; KSPEC-004 A-8).
2. `Certificate.EncryptedPrivateKeyPem` field observed with no traced writer — dormant
   schema or unarchived behavior (KSPEC-004 §17 item 4; SR-S.011 Proposed).
3. Synthetic demo enrollment records / demo-enroll endpoint not distinguished in UI
   semantics beyond SR-D.009 (Partial, Low).
4. Hard-coded 60-second CRL lifetime (SR-S.012 Missing; SR-C.008 Partial) — prototype
   value with no policy hook.
5. `ApiKeyAuthenticationHandler` development bypass (SR-A.009) — implemented and
   documented, but no VT exercises the gating conditions.
6. XFCC header trusted from IP-set membership only (SR-S.005 Missing; KSPEC-004 P-2).
7. Hard-coded JWT dev-secret fallback with silent fallback behavior (no explicit warning for the API JWT fallback) (SR-S.007 / SR-A.006
   Missing).

### 9.4 Missing interface definitions

1. `OpenAPI.Client.yaml` empty; `OpenAPI.Schema.yaml` stale (SR-F-INT.003 Missing;
   KSPEC-005 open item) — no authoritative client-facing contract.
2. Pending-enrollment trigger matrix undefined: which CA backend states produce
   `IsPending` (SR-F-EST.011 Missing; KSPEC-005 ICD-EST.004 open item).
3. Self-service request authorization model unconfirmed (SR-Z.009 Proposed; KSPEC-005
   open item 6; SR-F-REG.007 Missing).
4. `POST /api/cas/{id}/test` role policy undefined (SR-Z.007 Partial; KSPEC-005 open
   item 4).
5. `csrattrs` response schema absent (SR-F-EST.014 Missing; KSPEC-004 P-3).
6. Prior unallocated interoperability prefix references were corrected in revision 0.2; current risk bindings use SR-F-INT or SR-F-CAI as appropriate.

### 9.5 Incomplete design decisions (KSPEC-004 §16.2/§16.3 and TBD items)

1. **A-1**: device status model lacks an `Untrusted`/`Retired` distinction — SR alignment
   not yet allocated.
2. **A-2 / SR-S.011**: whether the gateway archives device private keys — policy decision
   open; field present.
3. **A-3**: demo OID namespace `1.3.6.1.4.1.99999.*` — enterprise PEN decision open.
4. **A-4 / SR-S.012**: CRL lifetime policy — value TBD (KSPEC-007 TBD-1).
5. **A-5 / SR-C.008**: stale-revocation-data and established-session handling — normative
   statement pending.
6. **A-6 / SR-U.006**: audit export mapping (ATNA-style) — PROPOSED scope.
7. **A-7**: out-of-application renewal mechanism — KSPEC-007 TBD-3.
8. **A-9 / SR-D.002**: PostgreSQL provider exercise — KSPEC-007 TBD-5.
9. **P-1 / SR-B.007**: managed secret store; **P-2 / SR-S.005**: forwarder authentication;
   **P-3 / SR-F-EST.014**: `csrattrs`; **P-4 / SR-O.004**: metrics; **P-5**:
   activation-consumption concurrency guard; **P-6**: notification outbox (SR-R.003
   Partial); **P-7 / SR-V.002**: per-backend connector health.
10. Unconfirmed thresholds recorded as TBD, never invented (KSPEC-002 §16.5): SR-F-REG.007
    (abuse-control threshold), SR-S.006 (production rate limits), SR-S.008 (encryption
    approach), SR-C.003 (certificate-lifetime policy, TBD-001), SR-D.006 (retention
    period), SR-P.001 (production capacity), SR-P.003 (latency budget), SR-R.003 (delivery
    reliability), SR-V.004 (availability targets), SR-O.002 (log retention), SR-O.004
    (metrics format), SR-F-CAI.010 (secrets mechanism); SR-A.006 inherits the
    ≥32-character recommendation pending confirmation.

## 10. Identifier consistency verification

Checks performed while building every table (source documents re-read, not summarized
from memory):

| Check | Result |
|---|---|
| VT IDs against KSPEC-008 | All 60 VT records (VT-001 – VT-060) present, each with Test ID, linked requirements, preconditions, inputs, procedure, expected results, acceptance criteria, evidence status. KSPEC-008 was verified structurally complete and well-formed; **no integrity fixes were required**. |
| SR IDs against KSPEC-003 §21 register | All SR IDs used in §4–§8 exist in the register; area counts match §21.1 (154 total; Existing 108, Partial 25, Missing 18, Proposed 3). |
| UR→SR edges against KSPEC-003 `Source` fields | Every UR in §5 chains is cited by the named SR's `Source` field; no edge was added or removed. |
| UR IDs against KSPEC-002 | UR-001 – UR-038 all exist; UR statuses quoted from KSPEC-002 (§12: Existing 31, Partial 6, Missing 1; corrected per RF-003). |
| THR IDs against KSPEC-006 | THR-001 – THR-018 all exist; §7.1 resolves the prefix intent of KSPEC-006 §8 against specific SR IDs only where a VT record records the pairing. |
| ORK IDs against KSPEC-007 | ORK-001 – ORK-016 all exist; bands quoted from the KSPEC-007 §4 FMEA summary; the former interoperability prefix discrepancy is resolved in revision 0.2. |
| Repository paths | All paths cited in §4 Evidence columns appear in KSPEC-003 `Evidence` fields or the KSPEC-000 §10 evidence source map; none were asserted from outside the package documents. |
| No fabricated evidence | No test result, approval, or conformity claim is recorded; every execution claim is quoted or paraphrased from the KSPEC-008 evidence-status field with its tracker/artifact source. |

## 11. Summary statistics

| Metric | Value |
|---|---|
| SR requirements in register | 154 (Existing 108, Partial 25, Missing 18, Proposed 3) |
| SRs VT-linked | 114 (74.0%); 40 without direct VT linkage (§9.2) |
| UR stakeholder needs | 38; 38 traced to SRs; 35 with at least one VT (2 with none: UR-006, UR-035) |
| VT verification tests consumed | 60 (VT-001 – VT-060); 14 Proposed, 7 partially covered, 39 Covered per KSPEC-008 (corrected per RF-002) |
| THR threats | 18, all linked to SR prefixes and resolved specific SRs; 1 with no VT (THR-018) |
| ORK operational risks | 16, all linked to SR areas and resolved specific SRs; all 16 have at least one VT |

---

*End of KSPEC-009, version 0.2 (Draft). This document adds no requirements and records no
verification outcomes; it binds existing KSPEC-002/003/004/006/007/008 identifiers into a
single traceability matrix and reports every empty cell. Approval pending review per
KSPEC-000 §2.*
