# Kryptonian Formal Engineering Specification — Conventions and Controls

| Field | Value |
|---|---|
| Document ID | KSPEC-000 |
| Title | Specification Conventions and Document Controls |
| Version | 0.2 (Draft) |
| Status | Draft — pending review |
| Created | 2026-10-13 |
| Baseline commit | `682f996c8447afec13bc1fce5b7f59e6a06efa25` (branch `feat/windows-sandbox-installer`, "Release Windows gateway and portable enrollment demo 0.1.4") |
| Repository | https://github.com/AAPM-RT-SEC/RTSec.Kryptonian |
| Revision history | 0.1 initial draft; 0.2 code/document reconciliation, 2026-10-13 (KSPEC-016) |
| Source references | KSPEC-000 evidence map; KSPEC-016 reconciliation evidence |
| Author | Unassigned - no approval recorded |
| Reviewer | Unassigned - no approval recorded |
| Approver | Unassigned - no approval recorded |

This document defines the shared conventions, terminology, provenance classes, and
document-control rules that apply to every document in `docs/formal-specification/`.
It is the controlling reference for identifier allocation and evidence classification.

---

## 1. Document hierarchy and identifiers

| Document ID | File | Role |
|---|---|---|
| KSPEC-000 | `00-SPEC-CONVENTIONS.md` | Conventions and controls (this document) |
| KSPEC-001 | `01-PRODUCT-DEFINITION.md` | Product definition, scope, stakeholders, use cases |
| KSPEC-002 | `02-USER-REQUIREMENTS.md` | Stakeholder and user requirements (UR-nnn) |
| KSPEC-003 | `03-SOFTWARE-REQUIREMENTS.md` | Normative software requirements specification (SR-*) |
| KSPEC-004 | `04-SOFTWARE-DESIGN.md` | Architecture and design (AS-IS and TO-BE) |
| KSPEC-005 | `05-INTERFACE-CONTROL.md` | Interface control specification (ICD-nnn items) |
| KSPEC-006 | `06-CYBERSECURITY.md` | Cybersecurity architecture and threat model (THR-nnn) |
| KSPEC-007 | `07-OPERATIONAL-RISK.md` | Operational risk and reliability assessment (ORK-nnn) |
| KSPEC-008 | `08-VERIFICATION-VALIDATION.md` | Verification and validation specification (VT-nnn) |
| KSPEC-009 | `09-TRACEABILITY.md` | Requirements traceability matrix |
| KSPEC-010 | `10-GAP-ANALYSIS.md` | Engineering gap analysis (GAP-nnn) |
| KSPEC-011 | `11-STANDARDS-CONFORMANCE.md` | Security and standards conformance assessment |
| KSPEC-012 | `12-ENGINEERING-HANDOFF.md` | Engineering handoff specification |
| KSPEC-013 | `REVIEW-FINDINGS.md` | Independent review findings (RF-nnn) |
| KSPEC-014 | `README.md` | Package index and review sequence |
| KSPEC-015 | `FINAL-SUMMARY.md` | Package summary |
| KSPEC-016 | `13-CODE-DOCUMENT-RECONCILIATION.md` | Current source/document reconciliation |

Review sequence: 000 → 001 → 002 → 003 → (004, 005, 006, 007) → (008, 009, 010, 011) → 012 → 013 → 014.

## 2. Document control header (mandatory)

Every generated document shall begin with a table containing: document title, unique
document identifier (per §1), version, review status (Draft / In Review / Approved),
creation date, baseline repository commit SHA, revision history, source references,
and approval placeholders (Author / Reviewer / Approver with name–date–signature rows).

## 3. Product classification (binding for all documents)

Kryptonian is **enterprise healthcare IT cybersecurity infrastructure**: a certificate
enrollment, identity provisioning, and credential lifecycle management gateway.
It is **not** a regulated medical device and performs **no clinical functions**.
All documents shall:

- Exclude medical-device regulatory classification, FDA pathways, IEC 62304 lifecycle
  classification, ISO 14971 medical-device hazard analysis, and similar content.
- Preserve the distinction between **device identity** (X.509 credential holders),
  **certificate enrollment authorization** (approval to obtain a credential),
  **network trust** (TLS/mTLS peer authentication), **application-level
  authorization** (what a device may do at the application layer), and **clinical
  capability** (explicitly out of scope — never derived from Kryptonian state).
- Describe the device simulators (`Kryptonian.MedicalDevice`, `Kryptonian.DICOMTls`)
  as demonstration/reference clients, not as product deliverables for production.

## 4. AS-IS / TO-BE / PROPOSED classification

Every behavior description, architecture element, requirement, and interface shall be
classified:

- **AS-IS** — behavior present in the baseline commit (implementation source of evidence).
- **TO-BE** — intended, formally specified behavior of the target system (normative).
- **PROPOSED** — enhancement or design alternative requiring stakeholder approval
  (never presented as normative without approval).

Defects, shortcuts, weak controls, or limitations observed in the prototype are
recorded as findings and gaps — they are **never reproduced as intended requirements**.

## 5. Evidence provenance classes

All technical assertions shall carry one of these classes:

| Class | Meaning |
|---|---|
| VERIFIED | Confirmed through source inspection **and** executed tests (including recorded evidence under `artifacts/developer/verify-*/`). |
| CODE-OBSERVED | Established by source inspection only. |
| DOCUMENTED | Supported only by existing repository documentation; not confirmed in code. |
| INFERRED | Plausible conclusion from indirect evidence; not verified. |
| PROPOSED | Recommended behavior requiring stakeholder approval; not implemented. |
| UNKNOWN | Insufficient evidence; recorded as an open question or TBD. |

Prohibitions: no fabricated test results, approvals, standards conformity claims, or
stakeholder decisions. Documented-only claims are never upgraded to VERIFIED.

## 6. Requirement status classes

| Status | Meaning |
|---|---|
| Existing | Fully implemented at baseline with positive evidence. |
| Partial | Implemented incompletely or with known deviations. |
| Missing | Not implemented at baseline; required in TO-BE. |
| Proposed | Recommended addition requiring approval. |

## 7. Requirement identifier scheme

Identifiers are stable and never reused. Format: `<PREFIX>-<AREA>.<n>` where AREA is a
short mnemonic (e.g., `SR-F-REG.001`). Standalone prefixes use simple `-nnn` numbering.

| Prefix | Domain | Owner document |
|---|---|---|
| UR | Stakeholder/user requirements | KSPEC-002 |
| SR-F | Functional requirements (REG device registration, EST EST protocol, CLM certificate lifecycle, CAI CA integration, ADM administration, INT interoperability) | KSPEC-003 |
| SR-S | Security requirements | KSPEC-003 |
| SR-A | Authentication requirements | KSPEC-003 |
| SR-Z | Authorization requirements | KSPEC-003 |
| SR-C | Certificate lifecycle requirements | KSPEC-003 |
| SR-D | Data management requirements | KSPEC-003 |
| SR-P | Performance requirements | KSPEC-003 |
| SR-R | Reliability requirements | KSPEC-003 |
| SR-V | Availability requirements | KSPEC-003 |
| SR-U | Audit requirements | KSPEC-003 |
| SR-B | Deployment requirements | KSPEC-003 |
| SR-O | Operational maintenance requirements | KSPEC-003 |
| SR-X | Failure and recovery requirements | KSPEC-003 |
| THR | Threats (threat model entries) | KSPEC-006 |
| ORK | Operational risks | KSPEC-007 |
| VT | Verification tests | KSPEC-008 |
| GAP | Findings (gap analysis) | KSPEC-010 |
| RF | Independent review findings | KSPEC-013 |

The canonical register of all normative requirements (ID, statement, priority, status,
verification method) is maintained in `03-SOFTWARE-REQUIREMENTS.md` (Requirements
Register) and summarized in `09-TRACEABILITY.md`. Other documents reference
requirements by ID only and shall not redefine them.

## 8. Requirement record format (KSPEC-003)

Every normative requirement is presented with the fields: ID, Requirement (single
verifiable obligation using "shall"), Rationale, Source, Priority
(Critical/High/Medium/Low), Status (Existing/Partial/Missing/Proposed), Verification
(Test/Inspection/Analysis/Demonstration), Evidence (repository path, commit, doc
reference), Approval (Draft/Review Required/Approved).

Approval state is **never** inferred from implementation. At baseline, all requirements
are `Draft` or `Review Required`. Unconfirmed thresholds use `TBD` with a separate
recommendation and rationale.

## 9. Terminology (binding)

| Term | Definition |
|---|---|
| Gateway | The Kryptonian server application (API + admin portal). |
| Device | A networked client (often a medical device or its local agent) that enrolls via EST. |
| Activation code | One-time, short-lived credential issued at registration; consumed by the device to bootstrap EST authentication. |
| EST profile | Named policy bundle binding devices to a CA backend, subject-DN rules, key usage, validity, and trust policy. |
| CA backend | An institution-managed certificate authority reachable through a connector (Self-Signed, ACME, ADCS, EJBCA). |
| Connector | Implementation of the internal CA abstraction for one backend type. |
| Trust policy | Persisted profile data defining which issuers/anchors the gateway and clients accept. |
| Enrollment event | Persisted record of an enrollment/renewal/revocation attempt and outcome. |
| CRL | Certificate revocation list, issuer-scoped, published by the gateway or by the CA. |
| Renewal window | Pre-expiry period during which re-enrollment without re-approval is authorized. |
| Admin portal | React/TypeScript dashboard served by the API; a separate optional Blazor Server portal also exists. |
| DIMSE-TLS proxy | Demonstration component terminating mTLS for DICOM associations (AS-IS demonstration only). |

## 10. Evidence source map

| Evidence area | Primary repository sources |
|---|---|
| Product intent, positioning | README.md, Mvp.md, docs/HACKATHON-DELIVERABLE.md, docs/background.md, docs/ORNET-ALIGNMENT-2026-09-09.md, docs/CHANGES-AND-ORNET-ALIGNMENT.md |
| Architecture | docs/ARCHITECTURE.md, docs/flow.md, src/RTSec.Kryptonian.{Api,Application,Domain,Infrastructure,Web} |
| Interfaces / contracts | OpenAPI.Admin.yaml, OpenAPI.Client.yaml, OpenAPI.ESTProfiles.yaml, OpenAPI.Schema.yaml, OpenAPI.StatusObserve.yaml, docs/API-GUIDE.md, src/RTSec.Kryptonian.Api/Controllers/ |
| Device registration & lifecycle | src/RTSec.Kryptonian.Application/Services/, DevicesController.cs, docs/device-enrollment.md, docs/sequence-diagrams/*.mmd |
| EST protocol behavior | EstController.cs, EstProfilesController.cs, CrlController.cs, docs/device-enrollment.md |
| CA integration | src/RTSec.Kryptonian.Infrastructure/Harness/* (ADCS, EJBCA, Self-Signed connectors, SCEP client), src/RTSec.Kryptonian.Infrastructure/Acme/*, CaBackendsController.cs, docs/ca-backend-configuration.md, docs/adcs-real-backend-demo.md |
| Identity, users, RBAC | AuthController.cs, UsersController.cs, ApiKeyAuthenticationHandler.cs, docs/user-management.md, docs/admin-setup.md |
| Certificate lifecycle | CertificatesController.cs, CertificateExpiryWatcher.cs, CrlController.cs, enrollment events, docs/sequence-diagrams/07-* |
| Notifications | NotificationSettingsController.cs, src/RTSec.Kryptonian.Infrastructure/Notifications/*, docs/email-notifications.md |
| Device-side EST client | src/Kryptonian.MedicalDevice/Enrollment/*, docs/medical-device-demo.md, docs/README-enrollment-demo.md |
| DICOM TLS demonstration | src/Kryptonian.DICOMTls/*, docs/sequence-diagrams/08-*, tests/RTSec.Kryptonian.DimseTlsProxy.Tests/ |
| Deployment | docs/DEPLOYMENT.md, docker-compose*.yml, deploy/, installer/, docs/windows-installation.md, docs/windows-sandbox.md |
| Executed-test evidence | artifacts/developer/verify-*/evidence.json, tests/ (8 test projects), .github/workflows/ |
| Known limits & boundaries | docs/IMPLEMENTATION-TRACKER.md (deliberate boundaries section), README.md disclaimer |

## 11. Verification method abbreviations

T = Test, I = Inspection, A = Analysis, D = Demonstration (ISO/IEC/IEEE 29148 usage).

## 12. Diagrams

Architecture, sequence, and state-machine diagrams use Mermaid fenced blocks unless
PlantUML is required for state-machine complexity; Mermaid is the default.

## 13. Restrictions inherited from the assignment

- No application source code modifications; documentation artifacts only.
- No refactoring, defect fixing, or feature implementation.
- No fabricated evidence, approvals, or conformity claims.
- Prototype behavior never becomes normative without explicit review and approval.

## 14. Publication controls

Markdown is authoritative editable source. Production PDFs preserve Draft status and
are generated using `tools/build_pdf.py`. The source-hash publication manifest
identifies the exact Markdown revision independently of the application baseline SHA.
Use KSPEC-016 and the current disposition section in KSPEC-013 for current review status;
original review passages are historical evidence.
