# Kryptonian Stakeholder and User Requirements

| Field | Value |
|---|---|
| Document title | Stakeholder and User Requirements (UR-nnn) |
| Document ID | KSPEC-002 |
| Version | 0.2 (Draft) |
| Review status | Draft — pending review |
| Created | 2026-10-13 |
| Baseline repository commit | `682f996c8447afec13bc1fce5b7f59e6a06efa25` (branch `feat/windows-sandbox-installer`, "Release Windows gateway and portable enrollment demo 0.1.4") |
| Repository | https://github.com/AAPM-RT-SEC/RTSec.Kryptonian |
| Revision history | 0.1 — 2026-10-13 — Initial draft: 38 user requirements derived from KSPEC-001 product definition (Author: requirements engineering team). |
| Source references | KSPEC-000 (conventions); KSPEC-001 (product definition); README.md; Mvp.md; docs/IMPLEMENTATION-TRACKER.md; docs/background.md; docs/ORNET-ALIGNMENT-2026-09-09.md; docs/CHANGES-AND-ORNET-ALIGNMENT.md; docs/flow.md; docs/ARCHITECTURE.md; docs/device-enrollment.md; docs/user-management.md; docs/ca-backend-configuration.md; docs/medical-device-demo.md; docs/DEPLOYMENT.md |
| Author | Name: TBD — Date: TBD — Signature: TBD |
| Reviewer | Name: TBD — Date: TBD — Signature: TBD |
| Approver | Name: TBD — Date: TBD — Signature: TBD |
| Reconciliation revision | 0.2 - 2026-10-13 - source/document audit and publication controls (KSPEC-016) |

This document defines the stakeholder and user requirements (UR-nnn) for the
RTSec.Kryptonian Certificate Enrollment Gateway. Each UR is grouped by stakeholder and
carries: ID, statement, rationale, source, priority, status, verification method, and
provenance class per KSPEC-000 §5–§6 and §11. These URs are stakeholder-level
requirements; they are elaborated into normative software requirements (SR-*) only in
KSPEC-003 (KSPEC-000 §7). UR identifiers are stable and never reused.

Verification method abbreviations (KSPEC-000 §11): T = Test, I = Inspection,
A = Analysis, D = Demonstration.

**No UR in this document constitutes a standards-conformity claim** (EST, SDC, SDPi,
ATNA, DICOM TLS) or a regulatory classification; the gateway is healthcare IT
cybersecurity infrastructure, not a medical device (KSPEC-000 §3; KSPEC-001 EXC-005).

---

## 1. Requirement conventions

- **Statement form**: each UR is a single verifiable obligation using "shall".
- **Status classes** (KSPEC-000 §6): Existing (implemented at baseline with positive
  evidence), Partial (incompletely implemented or with known deviations), Missing
  (required in TO-BE, not implemented at baseline), Proposed (recommended addition
  requiring approval).
- **Provenance classes** (KSPEC-000 §5): VERIFIED, CODE-OBSERVED, DOCUMENTED, INFERRED,
  PROPOSED, UNKNOWN. Documented-only claims are never upgraded to VERIFIED.
- **Traceability note**: names the KSPEC-001 element (UC-*, SCN-*, ASM-*, EXC-*,
  §section) the UR elaborates and the expected SR elaboration area in KSPEC-003.
  SR identifiers are not allocated here.
- **Priorities**: Critical / High / Medium / Low.

---

## 2. Stakeholder-to-requirement map

| Stakeholder | UR range | Theme |
|---|---|---|
| Hospital IT administrators | UR-001 – UR-007 | Onboarding simplicity, inventory, backend configuration, administration |
| Hospital cybersecurity personnel | UR-008 – UR-015 | Policy enforcement, trust removal, revocation, audit, notifications, secrets, RBAC |
| Device manufacturers | UR-016 – UR-019 | Standards-based single-protocol enrollment across hospital CA choices |
| Device software developers | UR-020 – UR-023 | Enrollment client behavior, identity binding, renewal, error semantics |
| Clinical engineering | UR-024 – UR-026 | Expiry visibility, renewal automation, credential/clinical separation |
| Healthcare system integrators | UR-027 – UR-029 | API automation, deployment options, health monitoring |
| Enterprise PKI administrators | UR-030 – UR-033 | CA connectors, certificate policy profiles, trust policy, issuance policy enforcement |
| AAPM RT-SEC / OR.NET research community | UR-034 – UR-036 | Research alignment, no competing protocol, interoperability testing as research goal |
| Cross-cutting (all stakeholders) | UR-037 – UR-038 | Data portability and maintainability |

## 3. Requirements — Hospital IT administrators

| ID | Requirement | Rationale | Source | Priority | Status | Verification | Provenance | Traceability note |
|---|---|---|---|---|---|---|---|---|
| UR-001 | The gateway shall allow an administrator to register a device by entering only an administrative alias and to obtain a one-time activation code for that device, without manually entering the device certificate identity. | Minimizes onboarding effort and transcription errors; device identity is supplied authoritatively by the device itself. | docs/device-enrollment.md; README.md (Device Registration Center) | High | Existing | D | DOCUMENTED | Elaborates KSPEC-001 UC-01, SCN-01; SR-F-REG area in KSPEC-003. |
| UR-002 | The gateway shall generate activation codes that are one-time, time-limited, stored hashed, consumed on validated bootstrap acceptance before CA issuance, and rejected on replay. | The activation code is a bootstrap secret; reuse would allow identity impersonation. | docs/device-enrollment.md (validation rules); IMPLEMENTATION-TRACKER.md (one-time activation replay rejected) | Critical | Existing | T | VERIFIED (replay rejection per executed evidence) | Elaborates UC-01/UC-02, SCN-02; SR-A / SR-S area. |
| UR-003 | The gateway shall maintain a centralized device inventory showing each device's status (pending, active, untrusted, retired), assigned profile, identity metadata, and certificate state. | Administrators need one operating surface for the device estate and trust state. | docs/flow.md (device database model); README.md | High | Existing | D | DOCUMENTED | Elaborates UC-05; SR-D / SR-F-REG area. |
| UR-004 | The gateway shall allow an administrator to configure multiple CA backends through the admin portal using guided fields or an advanced JSON configuration. | Hospitals use different CA technologies; configuration must not require code changes. | docs/ca-backend-configuration.md | High | Existing | D | DOCUMENTED | Elaborates UC-06; SR-F-CAI area. |
| UR-005 | The gateway shall support testing a configured CA backend before activation and shall route each enrollment to the enabled CA backend bound to its resolved EST profile without the device sending a backend identifier. | Prevents misconfigured backends from receiving live enrollments; preserves the device-side single-protocol abstraction. | docs/ca-backend-configuration.md (activating/testing sections) | High | Existing | T | DOCUMENTED | Elaborates UC-06, SCN-06; SR-F-CAI area. |
| UR-006 | The gateway shall provide a dashboard summarizing CA backend state, registered device counts, enrollment outcomes, and backend evidence. | Administrators need rapid assessment of gateway health without drilling through pages. | README.md (Gateway dashboard) | Medium | Existing | D | DOCUMENTED | Elaborates UC-05; SR-F-ADM area. |
| UR-007 | The gateway shall support first-time administrator setup through an initial-account wizard or environment-seeded initial administrator, and shall prevent deletion of the last active SystemAdmin. | Prevents lockout on first deployment and after administrative mistakes. | docs/user-management.md (First-Time Setup; Delete a user) | High | Existing | T | DOCUMENTED | Elaborates UC-08; SR-Z / SR-B area. |

## 4. Requirements — Hospital cybersecurity personnel

| ID | Requirement | Rationale | Source | Priority | Status | Verification | Provenance | Traceability note |
|---|---|---|---|---|---|---|---|---|
| UR-008 | The gateway shall evaluate device trust policy itself and shall reject certificate requests from unknown, unactivated, untrusted, or retired devices before any contact with the CA backend. | The gateway, not the CA, owns the medical-device trust decision; prevents unauthorized devices from obtaining credentials. | docs/flow.md (Trust Policy Role; walkthrough) | Critical | Existing | T | DOCUMENTED | Elaborates UC-02/UC-04, SCN-02; SR-Z area. |
| UR-009 | The gateway shall, upon administrative trust removal, deny subsequent enrollment and renewal attempts for the device and record a blocked enrollment event with a reason. | Decommissioned or compromised devices must not acquire fresh credentials. | docs/flow.md (Trust Removal Flow); docs/HACKATHON-DELIVERABLE.md (device removal UI) | Critical | Existing | T | DOCUMENTED | Elaborates UC-04, SCN-05; SR-F-CLM / SR-C area. |
| UR-010 | The gateway shall support certificate revocation with issuer-scoped signed CRL publication, and shall fail closed when revocation is not supported by the configured remote CA backend rather than falsely reporting success. | Revocation must remain trustworthy; silent success on unsupported backends would mislead operators. | IMPLEMENTATION-TRACKER.md (PR 46; local-CA CRL demonstrated; unsupported remote revocation fails closed) | Critical | Partial | T | VERIFIED (local-CA revocation; remote ADCS/EJBCA revocation interoperability unverified) | Elaborates UC-04, SCN-05; SR-F-CLM / SR-C area; remote-backend revocation is a gap-analysis candidate (KSPEC-010). |
| UR-011 | The gateway shall record an audit record for every enrollment, renewal, and revocation decision, including requester identity, source address, CSR subject, outcome, and reason. | Security teams must be able to reconstruct who enrolled what, from where, through which backend. | docs/flow.md (Enrollment Events); Mvp.md §8 ("audit everything") | High | Partial | I | DOCUMENTED | Elaborates UC-05, SCN-02; SR-U area. |
| UR-012 | The gateway shall notify configured administrators by email when device certificates are not renewed as expected or approach expiry. | Non-renewal is an early signal of device problems; expiry otherwise leads to outages. | README.md (Renewal monitoring); docs/email-notifications.md | High | Partial | D | DOCUMENTED (delivery behavior at baseline UNKNOWN — TBD-002 in KSPEC-001) | Elaborates UC-09, SCN-04; SR-F-CLM / SR-C / SR-O area. |
| UR-013 | Connector credentials (CA service-account secrets, ACME account keys, CA key passwords) shall be supplied through a secure secrets mechanism rather than plain configuration files. | Leakage of CA-issuing credentials compromises the institution's PKI. | Mvp.md §8; TBD-005 in KSPEC-001 | High | Missing | I | PROPOSED (baseline uses environment variables/plain config; store integration not implemented) | Elaborates ASM-006/TBD-005; SR-B / SR-S area. |
| UR-014 | The gateway shall rate-limit EST endpoints to resist enrollment flooding. | The device-facing endpoints are exposed to the hospital network and must tolerate abusive traffic. | docs/ARCHITECTURE.md (rate limiting via AspNetCoreRateLimit) | Medium | Existing | T | CODE-OBSERVED (configuration documented; effectiveness not separately evidenced) | Elaborates KSPEC-001 §5; SR-S area. |
| UR-015 | The gateway shall enforce role-based access control with at least three roles (read-only, device administration, full system administration) and shall restrict device modification and system configuration to the appropriate roles. | Least privilege separates operational duties (device ops vs. system configuration) and read-only audit use. | docs/user-management.md (Roles) | Critical | Existing | T | DOCUMENTED | Elaborates UC-08, KSPEC-001 §4; SR-Z area. |

## 5. Requirements — Device manufacturers

| ID | Requirement | Rationale | Source | Priority | Status | Verification | Provenance | Traceability note |
|---|---|---|---|---|---|---|---|---|
| UR-016 | The gateway shall expose a single standards-based device enrollment protocol, EST (RFC 7030), that any conforming device client can use for enrollment and renewal regardless of which hospital CA backend is configured. | Manufacturers cannot implement custom logic per hospital CA; one open protocol is the core interoperability proposition. | README.md; Mvp.md; docs/background.md (desired pattern); RFC 7030 | Critical | Existing | D | DOCUMENTED | Elaborates UC-02/UC-03, KSPEC-001 §2; SR-F-EST area. |
| UR-017 | The gateway shall distribute CA certificates/trust anchors to devices via the EST `cacerts` operation for the selected profile. | Devices must authenticate the enrollment service and validate issued chains without out-of-band CA distribution where possible. | docs/ARCHITECTURE.md (EST endpoints); docs/medical-device-demo.md (trusted gateway CA selection) | High | Existing | T | DOCUMENTED | Elaborates UC-02; SR-F-EST / SR-F-INT area. |
| UR-018 | The gateway shall map device enrollment requests to the hospital-selected CA backend and its native protocol, so that device-side code requires no CA-specific integration. | Preserves the manufacturer's single implementation while hospitals keep CA choice; the gateway is the adaptation layer. | docs/flow.md (Responsibility Split); docs/background.md | Critical | Existing | A | DOCUMENTED | Elaborates UC-06, SCN-06; SR-F-CAI area. |
| UR-019 | The gateway's EST path shall be usable by independent clients using standard authentication only; any gateway-specific compatibility headers shall remain optional, documented as legacy compatibility, and shall not become an interoperability requirement. | A proprietary-header requirement would recreate the vendor lock-in the gateway exists to remove. | IMPLEMENTATION-TRACKER.md (independent curl HTTP-Basic EST demonstrated; legacy headers "must not become a new interoperability requirement") | High | Partial | T | VERIFIED (independent curl EST over real HTTPS) | Elaborates KSPEC-001 §12 enrollment-protocol row, EXC-004; SR-F-INT / SR-F-EST area. |

---

## 6. Requirements — Device software developers

| ID | Requirement | Rationale | Source | Priority | Status | Verification | Provenance | Traceability note |
|---|---|---|---|---|---|---|---|---|
| UR-020 | The gateway shall bind the enrolled device identity to the pending registration by validating that the CSR subject CN is present, unique among registered devices, and accompanied by device metadata including a serial number. | Prevents duplicate or ambiguous device identities and binds certificates to the intended registry entry. | docs/device-enrollment.md (validation rules); IMPLEMENTATION-TRACKER.md (PR 38 authenticated enrollment identity binding) | High | Existing | T | DOCUMENTED (PR 38 behavior VERIFIED per tracker) | Elaborates UC-02; SR-F-REG area. |
| UR-021 | The gateway shall allow an active, non-revoked device to renew its certificate by authenticating with its existing device certificate via EST `simplereenroll`, without re-presenting the activation code or requiring administrator approval, preserving the approved identity. | Renewal must be automatic and unattended; re-approval would defeat the automation goal. | docs/device-enrollment.md (Renewal And Future Signing); IMPLEMENTATION-TRACKER.md (PR 44; renewal uses native TLS client certificate, generates new key, preserves approved identity) | Critical | Existing | T | VERIFIED (renewal behavior per executed evidence) | Elaborates UC-03, SCN-03; SR-F-CLM / SR-C area. |
| UR-022 | The gateway shall serve EST operations under named, profile-scoped paths (`/.well-known/est/{profile}/...`) and shall reject requests for unknown or disabled profile labels. | Profile-scoped paths allow per-policy enrollment; unknown labels must not silently fall through to a default policy. | docs/medical-device-demo.md (EST profile URL usage); IMPLEMENTATION-TRACKER.md (PR 37 unknown-label rejection) | Medium | Existing | T | VERIFIED (profile issuer routing/unknown-label rejection per tracker) | Elaborates UC-07; SR-F-EST area. |
| UR-023 | The gateway shall return distinguishable error outcomes for failed device operations (e.g., invalid/expired/used activation code, unknown or disabled profile, unauthorized renewal) that client developers can diagnose. | Developers need actionable failures; undifferentiated errors raise support burden and mask security events. | docs/medical-device-demo.md (connection troubleshooting: 404/401/403 semantics) | Low | Existing | I | DOCUMENTED | Elaborates SCN-01/SCN-02; SR-F-EST / SR-X area. |

## 7. Requirements — Clinical engineering

| ID | Requirement | Rationale | Source | Priority | Status | Verification | Provenance | Traceability note |
|---|---|---|---|---|---|---|---|---|
| UR-024 | The gateway shall provide visibility of device certificate validity periods and expiry so that staff can identify devices at risk of losing network trust. | Expired credentials cause transport failures that present as device "network problems"; early visibility enables planned maintenance. | README.md (certificate expiry tracking in Devices page); docs/flow.md (certificates table) | High | Existing | D | DOCUMENTED | Elaborates UC-05/UC-09; SR-F-ADM / SR-D area. |
| UR-025 | The gateway shall enable automatic renewal for approved devices so that credentials remain valid without manual certificate installation on the device. | Manual installs are error-prone and scale poorly; automation removes a primary cause of expired-certificate outages. | README.md (automatic renewal); Mvp.md | High | Existing | D | DOCUMENTED | Elaborates UC-03, SCN-03; SR-F-CLM / SR-C area. |
| UR-026 | The gateway's device and certificate status shall not be presented as, and shall not be derivable as, a statement of a device's clinical service status, clinical capability, or regulatory status. | Prevents staff from misreading an operational TLS identity as clinical clearance; preserves the identity/capability boundary. | KSPEC-000 §3 (binding); docs/CHANGES-AND-ORNET-ALIGNMENT.md (renewal ≠ recertification) | Critical | Existing | I | DOCUMENTED | Elaborates KSPEC-001 §2.1, EXC-001; SR-Z / SR-F-ADM area. |

---

## 8. Requirements — Healthcare system integrators

| ID | Requirement | Rationale | Source | Priority | Status | Verification | Provenance | Traceability note |
|---|---|---|---|---|---|---|---|---|
| UR-027 | The gateway shall expose an authenticated administrative REST API, including API-key authentication carrying the generating user's role, for scripted device and configuration management. | Hospitals automate estate management; browser-only administration does not integrate with runbooks, CI, or configuration tooling. | docs/user-management.md (API Keys); docs/API-GUIDE.md | High | Existing | T | DOCUMENTED | Elaborates KSPEC-001 §4 (scripted clients); SR-A / SR-Z area. |
| UR-028 | The gateway shall be deployable as containers (Docker Compose, Kubernetes manifests) and as a Windows package, with production deployment guidance. | Hospitals have heterogeneous infrastructure; deployment flexibility determines adoptability. | docs/DEPLOYMENT.md; docs/medical-device-demo.md (Windows package) | Medium | Existing | I | DOCUMENTED (Windows installer path and PostgreSQL in practice: UNKNOWN — TBD-006 in KSPEC-001) | Elaborates §5; SR-B area. |
| UR-029 | The gateway shall expose a health-check endpoint reporting the health of its components (e.g., database, CA connectors). | Integrators and monitoring systems need a liveness/readiness probe for orchestration. | docs/DEPLOYMENT.md (health checks); docs/ARCHITECTURE.md | Medium | Existing | T | DOCUMENTED | Elaborates SCN-09 context; SR-V area. |

---

## 9. Requirements — Enterprise PKI administrators

| ID | Requirement | Rationale | Source | Priority | Status | Verification | Provenance | Traceability note |
|---|---|---|---|---|---|---|---|---|
| UR-030 | The gateway shall connect to institution-managed CA backends through pluggable connectors for Self-Signed, ACME (RFC 8555), ADCS (SCEP-compatible service), and EJBCA (REST) types. | These represent the realistic hospital CA landscape the abstraction must cover. | docs/ca-backend-configuration.md (supported backends); docs/HACKATHON-DELIVERABLE.md | High | Existing | T | DOCUMENTED (ADCS/EJBCA hospital interop unverified — see UR-010 note) | Elaborates UC-06; SR-F-CAI area. |
| UR-031 | The gateway shall support named EST profiles as policy bundles binding devices to a CA backend with subject-DN rules, key usage, validity, and trust policy. | Certificate policy must be administratively controlled by the institution, not hard-coded. | KSPEC-000 §9 (EST profile terminology); docs/ca-backend-configuration.md (common setup sequence) | Critical | Existing | I | DOCUMENTED | Elaborates UC-07; SR-F-CAI / SR-F-CLM / SR-C area. |
| UR-032 | The gateway shall persist EST profile trust policy across restarts and shall apply trust-list changes additively, preserving omitted trust lists on partial updates. | Loss or silent narrowing of trust policy on restart or update would break enrolled devices unpredictably. | IMPLEMENTATION-TRACKER.md (PR 41 durable trust policy; PR 47 partial updates preserve omitted trust lists) | High | Existing | T | VERIFIED (restart persistence on populated SQLite per tracker) | Elaborates UC-07, SCN-08; SR-D / SR-F-CLM / SR-C area. |
| UR-033 | The gateway shall enforce issuance policy on both the submitted CSR and the returned certificate (rejecting, e.g., unauthorized subject/SAN/EKU content) before delivering credentials to the device. | A malicious or faulty client must not obtain credentials whose identity or purpose exceeds its approved policy. | IMPLEMENTATION-TRACKER.md (PR 43 CSR and returned-certificate policy enforcement; ORNET-ALIGNMENT test matrix: unauthorized SAN/EKU) | Critical | Existing | T | VERIFIED (per tracker PR 43) | Elaborates UC-02/UC-07; SR-S / SR-F-CLM / SR-C area. |

## 10. Requirements — AAPM RT-SEC / OR.NET research community

| ID | Requirement | Rationale | Source | Priority | Status | Verification | Provenance | Traceability note |
|---|---|---|---|---|---|---|---|---|
| UR-034 | The project shall position the gateway as an RT-SEC reference implementation and test environment contributing operational DICOM credential-lifecycle evidence (enroll, renew, use for DICOM TLS, rotate, deny after trust withdrawal) to the OR.NET / IEEE 11073 SDC enrollment effort, without defining a competing enrollment protocol. | Avoids fragmenting the medical-device enrollment landscape; contributes implementation evidence where OR.NET is developing its own EST-based direction. | README.md; docs/ORNET-ALIGNMENT-2026-09-09.md; docs/CHANGES-AND-ORNET-ALIGNMENT.md (§recommended decisions 1–2) | High | Existing | I | DOCUMENTED | Elaborates KSPEC-001 §2, EXC-004; SR-F-INT area (research positioning). |
| UR-035 | All project communications and specification documents shall state explicitly that no OR.NET adoption, SDC conformity, DICOM TLS certification, or standards-conformance status is claimed, and that interoperability with OR.NET/IEEE 11073 SDC work is a research goal only. | Prevents overstating maturity; alignment is with correspondence-reported working-group intentions, not adopted specifications. | README.md disclaimer; docs/ORNET-ALIGNMENT-2026-09-09.md (review limits); IMPLEMENTATION-TRACKER.md (deliberate boundaries) | High | Existing | I | DOCUMENTED | Elaborates EXC-002/EXC-005; governing rule for KSPEC-011. |
| UR-036 | The project shall maintain an interoperability testing approach in which independent client/server pairs exercise the real network boundary (enrollment authentication, issuer trust, certificate identity/purposes, renewal, revocation timing, failure behavior), with test cases agreed jointly with OR.NET where possible. | Controller-mocked tests missed real defects at baseline; joint test vectors are the agreed path to credible interoperability evidence. | IMPLEMENTATION-TRACKER.md ("live checks exposed two bugs that mocked tests missed"); docs/ORNET-ALIGNMENT-2026-09-09.md (§proposed slices; recommended decision 3) | Medium | Partial | T | VERIFIED (developer verifier evidence exists for the gateway boundary); the joint OR.NET-agreed test vectors remain PROPOSED | Elaborates SCN-08/SCN-09; SR-F-INT / SR-V area; KSPEC-008 verification-spec input. |

---

## 11. Requirements — Cross-cutting (all stakeholders)

| ID | Requirement | Rationale | Source | Priority | Status | Verification | Provenance | Traceability note |
|---|---|---|---|---|---|---|---|---|
| UR-037 | The gateway shall persist all configured state (devices, certificates, profiles, backends, trust policy) durably so that a gateway restart preserves the configuration and records, with support for both SQLite and PostgreSQL persistence. | Configuration loss across restarts would silently revoke operational trust state; hospitals need an embeddable option (SQLite) and a server-grade option (PostgreSQL). | IMPLEMENTATION-TRACKER.md (gateway restart on populated SQLite preserved profile, issuer and devices); docs/DEPLOYMENT.md (PostgreSQL) | High | Partial | T | VERIFIED (SQLite restart persistence per tracker); PostgreSQL exercised: UNKNOWN | Elaborates §5, TBD-006; SR-D / SR-B area. |
| UR-038 | The gateway's software design shall keep CA backend integration behind a common internal connector abstraction so that new backend types can be added without changing device-facing behavior or business logic. | Sustains the multi-CA roadmap and the single-protocol device contract as backends evolve. | docs/ARCHITECTURE.md (Extension Points; factory pattern for CA connectors); Mvp.md §6.2 (connector interface) | Medium | Existing | A | CODE-OBSERVED (design documented; addition of a new connector not exercised at baseline) | Elaborates UC-06; SR-O / maintainability in KSPEC-003. |

## 12. Requirements coverage summary

| Coverage theme (task input) | Covering URs |
|---|---|
| Onboarding simplicity | UR-001, UR-002, UR-007, UR-020, UR-023 |
| Standards-based enrollment (RFC 7030) | UR-016, UR-017, UR-019, UR-022 |
| Multi-CA support | UR-004, UR-005, UR-018, UR-030, UR-038 |
| Centralized inventory and audit | UR-003, UR-006, UR-011, UR-024 |
| Renewal automation with monitoring alerts | UR-012, UR-021, UR-025 |
| Revocation and trust removal | UR-008, UR-009, UR-010 |
| RBAC | UR-015, UR-027 |
| Notification rules | UR-012 |
| Secure deployment | UR-013, UR-014, UR-028, UR-037 |
| Interoperability with DICOM TLS / OR.NET-SDC alignment (research goals, no conformity claims) | UR-034, UR-035, UR-036, UR-026 |
| Maintainability | UR-038, UR-022, UR-023 |

Total: **38 user requirements (UR-001 – UR-038)**.

| Priority | Count |
|---|---|
| Critical | UR-002, UR-008, UR-009, UR-010, UR-015, UR-016, UR-018, UR-021, UR-026, UR-031, UR-033 (11) |
| High | UR-001, UR-003, UR-004, UR-005, UR-007, UR-011, UR-012, UR-013, UR-017, UR-019, UR-020, UR-024, UR-025, UR-027, UR-030, UR-032, UR-034, UR-035, UR-037 (19) |
| Medium | UR-006, UR-014, UR-022, UR-028, UR-029, UR-036, UR-038 (7) |
| Low | UR-023 (1) |
| **Total** | **38** |

| Status | Count |
|---|---|
| Existing | 31 |
| Partial | 6 (UR-010, UR-011, UR-012, UR-019, UR-036, UR-037) |
| Missing | 1 (UR-013) |
| Proposed (as requirement origin) | covered by UR-013 provenance; no standalone Proposed-status URs at stakeholder level |

---

## 13. Open questions carried forward (TBD / UNKNOWN)

| ID | Open question | Related URs |
|---|---|---|
| TBD-001 | Production certificate-lifetime policy (outage tolerance vs. revocation objectives). | UR-021, UR-025, UR-031 |
| TBD-002 | Notification trigger set and delivery reliability at baseline. | UR-012 |
| TBD-003 | Clinical settings/bootstrap options in the forthcoming OR.NET white paper. | UR-034, UR-036 |
| TBD-004 | Location and adoption status of shared OR.NET/RT-SEC requirements and test vectors. | UR-035, UR-036 |
| TBD-005 | Production secrets-management mechanism for connector credentials. | UR-013 |
| TBD-006 | PostgreSQL persistence behavior in practice. | UR-028, UR-037 |

---

## 14. Traceability

- **Upstream**: every UR cites the KSPEC-001 element it elaborates in its traceability note
  (UC-01 – UC-10, SCN-01 – SCN-10, ASM-001 – ASM-007, EXC-001 – EXC-008, and §2/§4/§5/§12).
- **Downstream**: URs are to be elaborated into SR-* requirements in KSPEC-003
  (SR-F-REG, SR-F-EST, SR-F-CLM, SR-F-CAI, SR-F-ADM, SR-Z, SR-A, SR-S, SR-D, SR-U,
  SR-B, SR-O, SR-V, SR-X areas per KSPEC-000 §7). KSPEC-003 shall maintain the canonical
  requirements register including UR→SR mapping; this document does not redefine
  requirements there.

---

## 15. Status detail and evidence basis

This section records, for each status classification used above, what the evidence
basis is at the baseline commit. It is provided so reviewers can re-derive each status
and so that later documents (KSPEC-003, KSPEC-010) do not silently upgrade
Documented-only claims to Verified.

| UR(s) | Status | Evidence basis |
|---|---|---|
| UR-002, UR-019, UR-021, UR-022, UR-032, UR-033, UR-036 (partial) | Existing / Partial | Executed evidence recorded in docs/IMPLEMENTATION-TRACKER.md and `artifacts/developer/verify-*/evidence.json`: replay rejection, independent HTTP-Basic EST over real HTTPS, certificate-authenticated renewal with new key and preserved identity, profile routing/unknown-label rejection, trust-policy restart persistence, CSR/returned-certificate policy enforcement, and loopback proxy tests. These are Verified per KSPEC-000 §5. |
| UR-010 | Partial | Local-CA issuer-scoped signed CRLs and transactional revocation demonstrated with online revocation checking; remote ADCS/EJBCA revocation interoperability explicitly unverified and unsupported remote revocation fails closed. The Verified portion is local-CA only. |
| UR-012 | Partial | Renewal monitoring is a stated product capability (README.md) with notification infrastructure and settings documented; delivery behavior was not exercised in the reviewed evidence. |
| UR-013 | Missing | Secure secrets storage for connector credentials is recommended in Mvp.md §8 and flagged as TBD-005; baseline uses environment variables and configuration files. |
| UR-001, UR-003 – UR-007, UR-008, UR-009, UR-014 – UR-018, UR-020, UR-023 – UR-031, UR-034, UR-035, UR-038 | Existing | Behavior described in repository documentation (Device Registration Center, Roles, CA backend configuration, flow document, architecture, deployment) consistent with the evidence source map (KSPEC-000 §10). Classified DOCUMENTED or CODE-OBSERVED as noted per row; none upgraded to VERIFIED without executed evidence. |
| UR-011 | Partial | Selected lifecycle events are persisted, but early failures and revocation paths do not provide complete durable per-attempt audit coverage (CODE-OBSERVED; GAP-038, VT-059). |
| UR-037 | Partial | SQLite restart persistence Verified via tracker; PostgreSQL schema upgrades exist but persistence was not exercised (UNKNOWN). |

## 16. Constraints on elaboration (guidance for KSPEC-003)

The following constraints bind the elaboration of these URs into software requirements:

1. **No clinical capability derivation.** No SR may derive clinical capability,
   clinical authorization, or SDC role assignment from gateway state (KSPEC-000 §3;
   EXC-001, EXC-002; UR-026).
2. **No conformity smuggling.** SRs elaborating UR-016/UR-017/UR-019 shall phrase
   EST behavior as implementation behavior, not as RFC 7030 conformance claims;
   conformance assessment belongs to KSPEC-011 and may only reference the assessed
   evidence.
3. **Legacy headers non-normative.** Compatibility activation headers observed in the
   sample device client shall not be reproduced as mandatory interface requirements
   (EXC-004; UR-019).
4. **Revocation realism.** SRs elaborating UR-010/UR-009 shall preserve the
   fail-closed behavior for unsupported remote revocation and shall not promise instant
   mid-association termination (EXC-007).
5. **Unconfirmed thresholds.** Where a UR leaves a threshold or policy value
   unspecified (certificate lifetimes, notification triggers, rate-limit values), the
   SR shall use `TBD` with a separate recommendation and rationale, never an invented
   value presented as agreed (KSPEC-000 §8).
6. **Approval state.** All elaborated SRs inherit Draft / Review Required approval
   state; approval is never inferred from implementation status (KSPEC-000 §8).

## 17. Inter-requirement dependencies

| Dependency | Reason |
|---|---|
| UR-001 → UR-002, UR-020 | Registration and activation-code generation are meaningful only with the code-validation and identity-binding rules. |
| UR-008 → UR-009, UR-010 | Trust-policy evaluation is the gate that trust removal and revocation tighten. |
| UR-016 → UR-017, UR-019, UR-022 | The single-protocol contract depends on `cacerts` distribution, independent-client usability, and profile-scoped paths. |
| UR-004/UR-005 → UR-018 | Backend configuration and activation are the mechanism behind CA-agnostic device enrollment. |
| UR-031 → UR-032, UR-033 | Profile policy bundles are only trustworthy if durable and enforced on CSR and returned certificates. |
| UR-021 → UR-012, UR-024, UR-025 | Renewal automation is what makes expiry visibility and non-renewal alerting the correct compensating controls. |
| UR-015 → UR-027 | REST automation inherits the same RBAC model as the portal. |
| UR-030 → UR-038 | The connector abstraction is the maintainability precondition for the supported backend set. |
