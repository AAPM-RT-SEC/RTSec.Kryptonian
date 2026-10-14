# Kryptonian Formal Engineering Specification — Final Summary

| Field | Value |
|---|---|
| Document ID | KSPEC-015 |
| Title | Final Package Summary |
| Version | 0.2 (Draft) |
| Status | Draft — pending review |
| Created | 2026-10-13 |
| Baseline commit | `682f996c8447afec13bc1fce5b7f59e6a06efa25` (branch `feat/windows-sandbox-installer`, "Release Windows gateway and portable enrollment demo 0.1.4") |
| Revision history | 0.1 initial draft; 0.2 code/document reconciliation, 2026-10-13 (KSPEC-016) |
| Source references | KSPEC-000 evidence map; KSPEC-016 reconciliation evidence |
| Author | Unassigned - no approval recorded |
| Reviewer | Unassigned - no approval recorded |
| Approver | Unassigned - no approval recorded |

## 1. What was produced

A 17-document formal engineering specification package in `docs/formal-specification/`,
reverse-engineered from the repository at the baseline commit. The package covers
product definition, stakeholder and software requirements, architecture, interface
control, cybersecurity threat model, operational risk, V&V, traceability, gap
analysis, standards conformance, engineering handoff, independent review findings,
package index, and this summary.

## 2. Requirements extracted

| Measure | Value |
|---|---|
| Stakeholder/user requirements (KSPEC-002) | 38 (UR-001 – UR-038): 11 Critical, 19 High, 7 Medium, 1 Low |
| Software requirements (KSPEC-003) | 154 (SR-F-REG, SR-F-EST, SR-F-CLM, SR-F-CAI, SR-F-ADM, SR-F-INT, SR-S, SR-A, SR-Z, SR-C, SR-D, SR-P, SR-R, SR-V, SR-U, SR-B, SR-O, SR-X) |
| Priority distribution (SR) | 36 Critical, 71 High, 38 Medium, 9 Low |
| Threats cataloged | 18 (THR-001 – THR-018, STRIDE) |
| Operational risks | 16 (ORK-001 – ORK-016, FMEA with RPN bands) |
| Verification tests specified | 60 (VT-001 – VT-060) |
| Gap findings | 40 (GAP-001 – GAP-040) |
| Independent review findings | 24 (RF-001 – RF-024; original review plus reconciliation) |
| Open decisions registered | 20 (DEC-001 – DEC-020 in KSPEC-012 §10) |

## 3. Requirements by implementation status (KSPEC-003 register)

| Status | SR count | UR count |
|---|---|---|
| Existing | 108 | 31 |
| Partial | 25 | 6 |
| Missing | 18 | 1 |
| Proposed | 3 | 0 (recorded at provenance level) |

Traceability: 114 of 154 SRs (74.0%) are VT-linked; 40 have no direct VT link; 38 of 38 URs
trace to SRs (UR-007 is linked to initial setup and last-administrator protection); 18 of 18 threats and
16 of 16 operational risks are linked to requirements, with THR-018 lacking a VT; THR-015 links to proposed VT-059.

## 4. Major architecture findings

- Clean-architecture .NET 8 system: ASP.NET Core API (EST + admin) serving the React/TypeScript dashboard, with a separate optional Blazor Server portal, EF Core persistence (SQLite exercised; PostgreSQL
  implemented but unexercised), four CA connectors (Self-Signed, ACME, ADCS, EJBCA)
  behind a common connector abstraction, plus demonstration components
  (WPF EST client, DIMSE-TLS mTLS proxy).
- Strong AS-IS core guarantees (VERIFIED): standard HTTP-Basic EST bootstrap with
  one-time hashed activation codes and replay rejection; native TLS client-certificate
  re-enrollment with fresh keys and preserved identity; profile-scoped issuer routing
  with unknown-label 404 fail-closed behavior; issuer-scoped signed CRLs with
  transactional revocation failing closed on unsupported backends; race-safe
  activation consumption; restart persistence of trust policy.
## 5. Important cybersecurity gaps

1. Deployment blockers: unremediated high advisories in
   `SQLitePCLRaw.lib.e_sqlite3 2.1.6` and `System.Security.Cryptography.Xml 8.0.2`
   (THR-014, the only Critical residual risk).
2. Hard-coded JWT development fallback secret; legacy static SystemAdmin API keys
   (THR-007/009/016).
3. No lockout/brute-force throttling on admin authentication; 8-character minimum
   password policy.
4. XFCC client-certificate forwarding trusted from source IP only (no authenticated
   forwarder).
5. Hard-coded 60-second prototype CRL lifetime with anonymous CRL endpoint —
   revocation freshness is cadence-dependent.
6. Audit trail is plain database records without tamper-evidence or export.
7. Connector credentials (PFX passwords) may sit in plaintext configuration.
8. File-based CA key storage: host compromise equals CA compromise.

## 6. Interoperability limitations

- RFC 7030: `/cacerts`, `/simpleenroll`, `/simplereenroll` implemented (conformance
  judgments marked INFERRED; no conformity claimed). `/csrattrs`, `/serverkeygen`,
  `/fullcmc` not implemented; `/csrattrs` aligned to RFC 9908 if pursued.
- EST uses RFC 7030 request/response media types (`application/pkcs10`, `application/pkcs7-mime`); additional `text/plain` acceptance is a documented leniency. The device-record GUID is a site-specific Basic username convention, not a Basic wire-format deviation (RFC 7030 §§3.2.3, 4.2.1, 4.2.3).
- ADCS/EJBCA hospital enrollment and revocation interoperability remains UNVERIFIED;
  remote-CA revocation fails closed. EJBCA `/cacerts` sees an empty chain on a fresh scoped connector; earlier issuance in another request does not persist that cache.
- DICOM TLS (B.12/B.13-related) and OR.NET/IEEE 11073 SDC alignment are research
  goals only; no certification or adopted-profile claim is made.
- Contract artifacts drift: `OpenAPI.Client.yaml` is empty, `OpenAPI.Schema.yaml` is
  stale, EST paths exist in no OpenAPI file.

## 7. Outstanding engineering decisions (KSPEC-012 §10)

DEC-001 certificate-lifetime policy; DEC-002 notification triggers/reliability;
DEC-003/004 OR.NET white-paper scope and shared test vectors; DEC-005 secrets
mechanism; DEC-006 PostgreSQL selection; DEC-007 EST rate limits; DEC-008 forwarder
authentication; DEC-009 long-lived renewal/retirement policy; DEC-010 CRL lifetime;
DEC-011 device Untrusted vs Retired; DEC-012 key-archival/`EncryptedPrivateKeyPem`
fate; DEC-013 enterprise PEN/OID namespace; DEC-014 anonymous self-service model;
DEC-015 ATNA audit export/tamper protection; DEC-016 availability/capacity/latency
targets; DEC-017 backup RPO/RTO; DEC-018 advisory remediation plan; DEC-019 metrics
stack; DEC-020 retention periods.

## 8. Recommended implementation priorities (KSPEC-010 waves; KSPEC-012 roadmap)

- **Wave 0 / Phase 0 (deployment blockers):** dependency advisory remediation
  (GAP-005), remove JWT fallback secret with fail-closed startup (GAP-006),
  admin-auth lockout/rate limiting (GAP-007).
- **Phase 1:** core EST + self-signed CA + admin core with regenerated OpenAPI
  contracts and drift checks.
- **Phase 2:** renewal/revocation/notifications hardening (outbox, CRL policy,
  trust-withdrawal states).
- **Phase 3:** ADCS/EJBCA hospital interoperability with real evidence (first-time
  verification of THR-005/008).
- **Phase 4:** operational hardening — backup/restore rehearsals, secrets store,
  authenticated forwarders, metrics, audit tamper protection, component health.

## 9. Acceptance-criteria status (assignment §11)

1. Independent-engineer comprehension — met via KSPEC-001/003/004/005/012.
2. Mandatory functions as verifiable requirements — met (154 SRs, all with verification
   methods).
3. External integration contracts — met via KSPEC-005 (EST + admin + connector
   contracts; OpenAPI fragments embedded; contract-file regeneration is a tracked gap).
4. Security assumptions and trust boundaries — met via KSPEC-004 §5 and KSPEC-006.
5. Certificate lifecycle fully described — met via KSPEC-004 §8/§10 and KSPEC-003
   SR-C/SR-F-CLM.
6. Architecture/data-model consistency — met within the package (RF-001 reconciliation
   recorded).
7. Full traceability — met via KSPEC-009 with empty cells explicitly reported.
8. Measurable acceptance criteria — met via KSPEC-008 VTs and KSPEC-012 definition of
   done.
9. Material implementation gaps documented — met via KSPEC-010 (40 gaps).
10. Unresolved decisions identified, not guessed — met via TBD/UNKNOWN discipline and
    DEC register.

**Overall verdict:** the package is a technically sound, evidence-faithful **Draft**.
It is not approvable until stakeholder review, target verification and the TBD/DEC decisions are completed; documentation findings are dispositioned in KSPEC-013 and KSPEC-016. No application source code
was modified. No fabricated evidence, approvals, or conformity claims exist in the
package.

---

*End of KSPEC-015, version 0.2 (Draft).*
