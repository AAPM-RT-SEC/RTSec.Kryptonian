# Kryptonian Formal Engineering Specification — Engineering Gap Analysis

| Field | Value |
|---|---|
| Document title | Engineering Gap Analysis (GAP-*) |
| Document ID | KSPEC-010 |
| Version | 0.2 (Draft) |
| Review status | Draft — pending review |
| Created | 2026-10-13 |
| Baseline repository commit | `682f996c8447afec13bc1fce5b7f59e6a06efa25` (branch `feat/windows-sandbox-installer`, "Release Windows gateway and portable enrollment demo 0.1.4") |
| Repository | https://github.com/AAPM-RT-SEC/RTSec.Kryptonian |
| Revision history | 0.1 — 2026-10-13 — Initial draft: 37 gap findings consolidated from KSPEC-003 requirement statuses, KSPEC-005 interface deviations, KSPEC-006 threat-model boundaries, KSPEC-007 operational risks, and docs/IMPLEMENTATION-TRACKER.md (Author: gap analysis team). |
| Source references | KSPEC-000 (conventions); KSPEC-003 (SR register, §21); KSPEC-004 (design limitations); KSPEC-005 (interface deviations, open items §20); KSPEC-006 (THR-001–THR-018); KSPEC-007 (ORK-001–ORK-016); docs/IMPLEMENTATION-TRACKER.md; docs/ORNET-ALIGNMENT-2026-09-09.md; docs/CHANGES-AND-ORNET-ALIGNMENT.md; docs/adcs-real-backend-demo.md; docs/medical-device-demo.md; README.md; Mvp.md; artifacts/developer/verify-*/evidence.json |
| Author | Name: TBD — Date: TBD — Signature: TBD |
| Reviewer | Name: TBD — Date: TBD — Signature: TBD |
| Approver | Name: TBD — Date: TBD — Signature: TBD |
| Reconciliation revision | 0.2 - 2026-10-13 - source/document audit and publication controls (KSPEC-016) |

**Product classification (binding, KSPEC-000 §3).** Kryptonian is enterprise
healthcare IT cybersecurity infrastructure: an EST (RFC 7030) certificate enrollment,
identity provisioning, and credential lifecycle gateway. It is **not a regulated
medical device**, performs no clinical functions, and this document contains no
regulatory, IEC 62304, ISO 14971, or standards-conformity content.

---

## 1. Purpose and scope

This document (KSPEC-010) records the engineering gaps between the **baseline
implementation** (AS-IS behavior at commit `682f996`) and the **formal target
specification** (TO-BE behavior normatively specified by KSPEC-003, with design in
KSPEC-004 and interfaces in KSPEC-005). A gap is any requirement recorded with status
`Partial` or `Missing` in the KSPEC-003 Requirements Register, any AS-IS weak control
or deliberate boundary recorded in KSPEC-005/006/007 or docs/IMPLEMENTATION-TRACKER.md,
or any verification/recording deficiency identified while drafting this package.

This document:

- Categories every finding under the nine assessment dimensions defined in §2.
- Assigns stable `GAP-nnn` identifiers (owned by this document per KSPEC-000 §7).
- Cites supporting evidence with provenance classes (KSPEC-000 §5).
- Proposes corrections and links affected requirements by ID only (SR-*, UR-*).
- Defines a prioritized remediation ordering (§6).

This document does not modify source code, does not implement anything, and does not
fabricate approvals, test results, or conformity claims (KSPEC-000 §13).

## 2. Assessment dimensions

Every GAP record is assigned exactly one primary category:

| Category | Covers |
|---|---|
| Functional completeness | Specified protocol/product functions absent or incomplete at baseline. |
| Security | Weak or absent security controls, secrets handling, dependency exposure. |
| Interoperability | Cross-system behavior with devices, hospital CA backends, external clients; interoperability evidence. |
| Architecture | Structural weaknesses: trust-boundary coupling, component separation, lifecycle propagation. |
| Reliability | Failure modes affecting continuity of the credential lifecycle. |
| Performance | Absent or unmeasured capacity/latency targets. |
| Maintainability | Interface-contract currency, unresolved policy placeholders, dependency maintenance. |
| Testing | Verification coverage, executed-evidence gaps, test-suite health. |
| Operational readiness | Backup/restore, monitoring, deployment posture, database providers. |

## 3. Gap record format

Each gap follows the record field discipline of KSPEC-000 §8, adapted from a
requirement record to a finding record: ID, Finding, Evidence (provenance-classed),
Impact, Proposed correction, Affected requirements (SR-*/UR-* by ID only), Verification
criteria (T/I/A/D per KSPEC-000 §11), and Priority (§4).

## 4. Priority definitions

| Priority | Meaning |
|---|---|
| Critical | Deployment blocker or gateway-wide security exposure; must be remediated (or explicitly risk-accepted by stakeholders) before any non-lab deployment. |
| High | Materially weakens the target specification or a core lifecycle guarantee; schedule before production-planning review. |
| Medium | Track; remediate opportunistically or with a documented operational workaround. |
| Low | Acceptable for lab/demo scope with recorded rationale. |

Priorities in this document are the draft author's qualitative judgments pending
review; they align with, but do not override, the KSPEC-007 RPN bands or KSPEC-006
residual-risk ratings.

## 5. Baseline summary feeding this analysis

| Input | Value | Provenance |
|---|---|---|
| KSPEC-003 requirements | 154 total: 108 Existing, 25 Partial, 18 Missing, 3 Proposed | Register consistency checked (KSPEC-003 §21.1); not implementation execution evidence |
| KSPEC-006 threats | 18 THR entries; residual-risk clusters: administrative/secret-management defaults, XFCC proxy trust, revocation-data boundaries | VERIFIED (KSPEC-006 §9) |
| KSPEC-007 risks | 16 ORK entries; Critical band: ORK-008 (failed revocation), ORK-009 (clock skew), ORK-011 (upgrade failure) | VERIFIED (KSPEC-007) |
| Implementation tracker | 16 merged PRs (37–52); executed local evidence; deliberate boundaries section lists deployment blockers and unverified integrations | VERIFIED (docs/IMPLEMENTATION-TRACKER.md) |

---

## 6. Gap summary table

| ID | Category | Short title | Priority | Affected SRs (primary) |
|---|---|---|---|---|
| GAP-001 | Functional completeness | `/csrattrs` endpoint missing | Medium | SR-F-EST.014 |
| GAP-002 | Functional completeness | `/serverkeygen` endpoint missing | Low | SR-F-EST (area) |
| GAP-003 | Functional completeness | `/fullcmc` not implemented | Low | SR-F-EST (area) |
| GAP-004 | Functional completeness | Pending trigger matrix / Retry-After determinism undefined | Medium | SR-F-EST.011 |
| GAP-005 | Security | Unremediated high transitive dependency advisories (deployment blockers) | Critical | SR-S.014, SR-O.005 |
| GAP-006 | Security | JWT hard-coded dev-fallback signing secret | Critical | SR-S.007, SR-A.006 |
| GAP-007 | Security | No lockout/rate limiting on admin authentication | High | SR-A.001, SR-F-ADM.003 |
| GAP-008 | Security | Legacy static SystemAdmin API keys remain accepted | High | SR-A.004 |
| GAP-009 | Security | XFCC forwarder authenticated only by source IP | High | SR-S.005 |
| GAP-010 | Security | Hard-coded 60-second prototype CRL lifetime | High | SR-S.012, SR-C.008 |
| GAP-011 | Security | Connector credentials and secrets at rest in plain config | High | SR-F-CAI.010, SR-D.008, SR-S.008 |
| GAP-012 | Security | Audit records lack tamper-evidence and export | High | SR-U.005, SR-U.006 |
| GAP-013 | Security | Data Protection key-ring persistence incomplete | High | SR-S.010, SR-D.007 |
| GAP-014 | Security | Weak password policy; no MFA | Medium | SR-A (area) |
| GAP-015 | Security | Dev-mode revocation skip and plain-HTTP escape hatches | Medium | SR-A.009, SR-F-EST.012 |
| GAP-016 | Security | ACME DNS-01 challenge is a stub | Medium | SR-F-CAI.007 |
| GAP-017 | Security | Device demo exports unencrypted PFX/PEM private keys | Low | SR-F-INT.006 |
| GAP-018 | Interoperability | ADCS/NDES connector protocol gaps | High | SR-F-CAI.005 |
| GAP-019 | Interoperability | ADCS/EJBCA hospital interoperability (incl. revocation) unverified | High | SR-F-CAI.006 |
| GAP-020 | Interoperability | Legacy activation headers retained (compatibility only) | Medium | SR-F-EST.007, SR-F-INT.002 |
| GAP-021 | Interoperability | Media-type handling leniency/deviations on EST | Medium | SR-F-EST.003, SR-F-EST.013 |
| GAP-022 | Interoperability | Joint interoperability test vectors absent | Medium | SR-F-INT.008 |
| GAP-023 | Architecture | Bounded proxy lifetimes; no instant mid-association revocation | Medium | SR-C.008 |
| GAP-024 | Architecture | Admin listener dual-purpose (browser prompt / mTLS coupling) | Medium | SR-Z (area) |
| GAP-025 | Reliability | Notification delivery failures silently swallowed | High | SR-R.003, SR-F-CLM.006 |
| GAP-026 | Reliability | Renewal runs only while simulator app is open | High | SR-F-CLM.002, SR-O.003 |
| GAP-027 | Reliability | Clock-synchronization dependency unmitigated | High | SR-D.005, SR-C.008 |
| GAP-028 | Performance | Capacity/latency targets undefined and unmeasured | Medium | SR-P.001, SR-P.003 |
| GAP-029 | Maintainability | Stale/empty OpenAPI contract files | High | SR-F-INT.003, SR-F-INT.004 |
| GAP-030 | Maintainability | Unresolved TBD threshold cluster | Medium | SR-D.006, SR-V.004, SR-C.003, SR-P.003 |
| GAP-031 | Maintainability | No SBOM; dependency audit not whole-system | Medium | SR-S.014 |
| GAP-032 | Testing | Test-suite skips, analyzer warnings, mocked-vs-live divergence | Medium | SR-* (verification) |
| GAP-033 | Testing | No independent-client / negative-path matrix over real ingress | Medium | SR-F-INT.001 |
| GAP-034 | Operational readiness | No backup/restore procedure; upgrade lacks safety boundary | High | SR-X.007, SR-D.004 |
| GAP-035 | Operational readiness | Component-level health reporting and metrics missing | Medium | SR-V.002, SR-O.004 |
| GAP-036 | Operational readiness | PostgreSQL provider never exercised | High | SR-D.002 |
| GAP-037 | Operational readiness | Deployment posture defaults (loopback, unsigned installer, demo records) | Medium | SR-B (area) |
| GAP-038 | Security | Incomplete durable lifecycle audit | High | SR-C.010, SR-U.001/.002 |
| GAP-039 | Reliability | Consumed activation failure/pending recovery | High | SR-F-REG.002, SR-F-EST.010/.011, SR-R.004 |
| GAP-040 | Security | Unauthenticated forwarded client-IP attribution | High | SR-S.005/.006, SR-P.001, SR-U.001 |

## 7. Detailed findings

### 7.1 Functional completeness

#### GAP-001 — `/csrattrs` endpoint missing

- **Finding:** The gateway implements `cacerts`, `simpleenroll`, and `simplereenroll` but not the EST `csrattrs` endpoint (RFC 7030 §4.5.2); clients cannot discover profile-required subject DN components or extensions and learn them only out of band.
- **Evidence:** `src/RTSec.Kryptonian.Api/Controllers/EstController.cs` (no csrattrs route) CODE-OBSERVED; KSPEC-005 §3 (endpoint surface) VERIFIED; docs/ARCHITECTURE.md lists `/csrattrs` as an extension point DOCUMENTED.
- **Impact:** Profile policy (SAN types, key size) cannot be negotiated with independent EST clients; interoperability with RFC 7030-conformant clients that expect `/csrattrs` is degraded.
- **Proposed correction:** Implement profile-driven `/csrattrs` aligned with RFC 9908 (CSR-attribute clarification, January 2026, updates RFC 7030) once the profile schema defines the required attribute set. [PROPOSED]
- **Affected requirements:** SR-F-EST.014 (Missing); KSPEC-002 UR-023 area.
- **Verification criteria:** T — independent client requests `/csrattrs`, receives profile-encoded attributes, and builds a compliant CSR.
- **Priority:** Medium

#### GAP-002 — `/serverkeygen` endpoint missing

- **Finding:** Server-side key generation (RFC 7030 §4.5.3) is not implemented; all device keys are client-generated.
- **Evidence:** `EstController.cs` (no serverkeygen route) CODE-OBSERVED; docs/ARCHITECTURE.md (extension point list) DOCUMENTED; Mvp.md (serverkeygen "if you support later") DOCUMENTED.
- **Impact:** Deployments requiring centrally generated keys (hardware-backed proxies, key escrow policies) cannot use the gateway; noted as optional capability, not a defect for the current scope.
- **Proposed correction:** Decide as a stakeholder question whether server keygen is in scope; if adopted, treat the key material path as HSM-grade per KSPEC-006 THR-016. [PROPOSED]
- **Affected requirements:** SR-F-EST (area); SR-S.011 (Proposed).
- **Verification criteria:** T (if adopted) — serverkeygen round-trip with encrypted response and key-archival policy test.
- **Priority:** Low

#### GAP-003 — `/fullcmc` not implemented

- **Finding:** Full CMC enrollment (RFC 7030 §4.6) is not implemented; the gateway supports only the `simpleenroll`/`simplereenroll` subset.
- **Evidence:** KSPEC-005 §3 endpoint inventory (no fullcmc) VERIFIED; docs/ARCHITECTURE.md DOCUMENTED.
- **Impact:** Clients requiring CMC flows (e.g., some enterprise PKI toolchains) cannot interoperate; acceptable for the defined EST-subset scope if documented as a deviation.
- **Proposed correction:** Record `/fullcmc` as an explicit, documented deviation in the interface contract rather than silent absence. [PROPOSED]
- **Affected requirements:** SR-F-EST (area); SR-F-INT.003.
- **Verification criteria:** I — deviation recorded in KSPEC-005 and the OpenAPI/EST contract.
- **Priority:** Low

#### GAP-004 — Pending trigger matrix / Retry-After determinism undefined

- **Finding:** The gateway returns HTTP 202 with `Retry-After` for pending issuance (implemented, PR #44), but the per-backend conditions that produce a pending result and the `Retry-After` computation rule are undefined (`TBD`), so client retry behavior is not deterministic across CA backends.
- **Evidence:** `EstController.cs` `HandleEnrollmentResult` (202 + Retry-After) CODE-OBSERVED; SR-F-EST.011 (Missing, trigger matrix `TBD`) VERIFIED (KSPEC-003); KSPEC-005 ICD-EST.004 open item (which backend states produce IsPending — UNKNOWN).
- **Impact:** Clients cannot predict retry timing; diagnostics of stalled enrollments differ per backend; risk of indefinite retry loops.
- **Proposed correction:** Define a per-backend pending-trigger matrix and a documented `Retry-After` computation rule in KSPEC-005 and implement/verify per backend. [PROPOSED]
- **Affected requirements:** SR-F-EST.010 (Existing), SR-F-EST.011 (Missing).
- **Verification criteria:** A + T — matrix reviewed; per-backend test drives each documented pending condition and observes 202 + Retry-After.
- **Priority:** Medium

### 7.2 Security

#### GAP-005 — Unremediated high transitive dependency advisories (deployment blockers)

- **Finding:** The transitive NuGet dependency audit reports high advisories in `SQLitePCLRaw.lib.e_sqlite3` 2.1.6 and `System.Security.Cryptography.Xml` 8.0.2 that remain unremediated; the tracker classifies them as deployment blockers requiring a separately tested framework/native-provider update.
- **Evidence:** docs/IMPLEMENTATION-TRACKER.md ("deployment blockers… not evidence of a clean security audit") VERIFIED; docs/CHANGES-AND-ORNET-ALIGNMENT.md §6 DOCUMENTED; MailKit was patched (4.16.0 + BouncyCastle 2.6.2) but these two advisories remain DOCUMENTED.
- **Impact:** Known vulnerable components ship in gateway deployments; CI gates cover UI npm advisories only, not the .NET transitive tree.
- **Proposed correction:** Test and land a framework/native-provider update that clears both advisories; add a CI gate rejecting high/critical advisories across the .NET solution, not only the UI. [PROPOSED]
- **Affected requirements:** SR-S.014 (Partial), SR-O.005.
- **Verification criteria:** T — `dotnet list package --vulnerable --include-transitive` reports zero high/critical advisories; CI gate demonstrated.
- **Priority:** Critical

#### GAP-006 — JWT hard-coded dev-fallback signing secret

- **Finding:** The gateway falls back to a hard-coded development JWT signing constant when `Kryptonian:Auth:JwtSecret` is not provisioned (appsettings ships an empty string); tokens are forgeable when the fallback is active.
- **Evidence:** `src/RTSec.Kryptonian.Api/appsettings.json` (`"JwtSecret": ""`) CODE-OBSERVED; plans/user-management.md ("dev fallback constant with warning") DOCUMENTED; SR-S.007/SR-A.006 recorded Missing VERIFIED (KSPEC-003 §22); KSPEC-006 THR-007 VERIFIED.
- **Impact:** Any deployment omitting the secret accepts attacker-forged admin JWTs — elevation of privilege to SystemAdmin.
- **Proposed correction:** Fail closed at startup when no sufficiently entropic signing secret (≥32 characters) is provisioned; remove the fallback; warn-and-log only in explicitly flagged dev mode. [PROPOSED]
- **Affected requirements:** SR-S.007 (Missing), SR-A.006 (Missing).
- **Verification criteria:** T — startup without provisioned secret aborts; with secret, tokens validate; fallback constant absent from release binary (I).
- **Priority:** Critical

#### GAP-007 — No lockout/rate limiting on admin authentication

- **Finding:** Administrative login has no brute-force lockout or per-account throttling; EST endpoints are rate-limited (100 requests/minute general; 30 requests/minute per EST enroll endpoint at baseline, per docs/ARCHITECTURE.md) but the admin authentication surface is not; minimum password length is 8.
- **Evidence:** KSPEC-006 THR-009 (weakness: "no lockout (I), bootstrap exposure (C)") VERIFIED; KSPEC-006 §9 cluster 2 ("no brute-force lockout, minimum-8-character passwords") VERIFIED; docs/ARCHITECTURE.md rate-limit scope (EST paths) CODE-OBSERVED.
- **Impact:** Online password guessing against admin portal/API is unthrottled; combined with GAP-006 it becomes a gateway-compromise path.
- **Proposed correction:** Add account lockout/exponential backoff and rate limiting on `/api/auth/*`; raise password policy consistent with NIST SP 800-63B length guidance; add MFA option. [PROPOSED]
- **Affected requirements:** SR-A.001, SR-F-ADM.003, SR-A.010 area.
- **Verification criteria:** T — scripted repeated failed logins are throttled/locked; legitimate login unaffected.
- **Priority:** High

#### GAP-008 — Legacy static SystemAdmin API keys remain accepted

- **Finding:** The pre-database static API-key mechanism (`KRYPTONIAN__ADMINAPI__APIKEYS` environment variable) continues to grant SystemAdmin for backward compatibility, alongside hashed, revocable database API keys.
- **Evidence:** plans/user-management.md (backward-compatibility note) DOCUMENTED; SR-A.004 "Static compatibility API keys" (Existing, Medium) VERIFIED (KSPEC-003); KSPEC-006 THR-007 (weakness: legacy static keys) VERIFIED.
- **Impact:** Non-revocable, config-distributed SystemAdmin credentials undermine the API-key lifecycle (revocation, expiry, ownership); accidental exposure yields permanent gateway control.
- **Proposed correction:** Add a deprecation path: disable static keys by default in release configurations, require explicit opt-in with rotation reminder, and migrate deployments to database keys. [PROPOSED]
- **Affected requirements:** SR-A.004, SR-A.003.
- **Verification criteria:** T — static key rejected when disabled; documented migration path (I).
- **Priority:** High

#### GAP-009 — XFCC forwarder authenticated only by source IP

- **Finding:** The `X-Forwarded-Client-Cert` path trusts a fronting proxy that is authorized solely by connection source IP (KnownProxies); the proxy itself is not authenticated by mutual TLS or cryptographic means.
- **Evidence:** SR-S.005 (Missing; mechanism `TBD`) VERIFIED (KSPEC-003); KSPEC-004 PROPOSED item P-2; KSPEC-006 THR-017 (boundary: proxy trust assumption) VERIFIED; `CertificateForwardingGuard` tests reject direct spoofing (spoof test in scripts/verify-dev.ps1) VERIFIED.
- **Impact:** On routed networks a source-IP allow-list can be spoofed; a peer able to appear as the proxy can assert forged device identities for enrollment.
- **Proposed correction:** Select and implement an authenticated-forwarder mechanism (mTLS from proxy, shared-secret header, or proxy-attested chain binding) per SR-S.005; record the decision. [PROPOSED]
- **Affected requirements:** SR-S.005 (Missing), SR-S.004 (Existing).
- **Verification criteria:** T — unauthenticated forwarder rejected with spoofed XFCC; authenticated forwarder accepted; negative test over real TLS ingress.
- **Priority:** High

#### GAP-010 — Hard-coded 60-second prototype CRL lifetime

- **Finding:** Locally generated CRLs use a hard-coded 60-second prototype lifetime (`PrototypeCrlLifetime = 60 s`); the production CRL `nextUpdate` policy is an open stakeholder decision (KSPEC-007 TBD-1).
- **Evidence:** KSPEC-007 ORK-008 (`PrototypeCrlLifetime = 60 s` CODE-OBSERVED) VERIFIED; SR-S.012 "Configurable CRL lifetime" (Missing) VERIFIED (KSPEC-003); scripts/verify-dev.ps1 (90-second revocation check window) CODE-OBSERVED.
- **Impact:** The 60-second cadence is a demo value; a policy for production (lifetime, staleness alerting, peer stale-CRL behavior, OCSP option) is undefined, leaving revocation-propagation guarantees unquantified.
- **Proposed correction:** Make CRL lifetime configurable per profile/issuer; define production lifetime from outage tolerance and revocation objectives per docs/ORNET-ALIGNMENT-2026-09-09.md; add CRL-staleness monitoring. [PROPOSED]
- **Affected requirements:** SR-S.012 (Missing), SR-C.008 (Partial).
- **Verification criteria:** T — configured lifetime honored in issued CRL `nextUpdate`; A — propagation analysis documented; I — staleness alerting configured.
- **Priority:** High

#### GAP-011 — Connector credentials and secrets at rest in plain config

- **Finding:** CA connector credentials (ACME account keys, ADCS challenge passwords, EJBCA credentials), CA PFX passwords, and the JWT secret are supplied in plain configuration/environment or files (secrets.json); no secret vault or at-rest encryption exists.
- **Evidence:** SR-F-CAI.010 and SR-D.008 recorded Missing; SR-S.008 Missing (approach `TBD`) VERIFIED (KSPEC-003 §22); artifacts/developer/secrets.json (plaintext secrets) CODE-OBSERVED; KSPEC-006 THR-016 VERIFIED.
- **Impact:** Compromise of configuration files or host filesystem exposes CA credentials and signing material; secrets may leak into backups and logs.
- **Proposed correction:** Integrate a secret store (OS keychain/DPAPI, Azure Key Vault, or equivalent) for connector credentials and CA passwords; define the encryption approach for SR-S.008. [PROPOSED]
- **Affected requirements:** SR-F-CAI.010, SR-D.008, SR-S.008 (all Missing).
- **Verification criteria:** I — no plaintext credential in config; T — gateway starts with vault-provisioned secrets.
- **Priority:** High

#### GAP-012 — Audit records lack tamper-evidence and export

- **Finding:** Enrollment events, audit records, and blocked-attempt records are database rows without tamper evidence (no hash chaining or append-only store), and no audit-export capability exists; the briefing notes this is a separate conformance task from a database audit table (ATNA/DICOM audit mapping not implemented).
- **Evidence:** SR-U.005 (Missing), SR-U.006 (Missing) VERIFIED (KSPEC-003); KSPEC-006 THR-015 (boundary: no tamper-evidence) VERIFIED; docs/ORNET-ALIGNMENT-2026-09-09.md (ATNA/DICOM audit mapping as future work) DOCUMENTED.
- **Impact:** An attacker with database write access can erase or alter evidence of trust decisions and administrative actions without detection; repudiation risk.
- **Proposed correction:** Add hash-chained or append-only audit records, tamper detection, protected export, and a documented (PROPOSED) path toward ATNA-compatible audit event mapping. [PROPOSED]
- **Affected requirements:** SR-U.005, SR-U.006 (Missing); SR-U.001–SR-U.004 (Existing).
- **Verification criteria:** T — tamper attempt detected; I — export mechanism inspected; A — mapping analysis documented.
- **Priority:** High

#### GAP-013 — Data Protection key-ring persistence incomplete

- **Finding:** ASP.NET Data Protection key-ring persistence is only partially specified: activation-code hashing and dashboard protection depend on a key ring whose persistence across installs/containers is not fully guaranteed, so restarts may invalidate protected state.
- **Evidence:** SR-S.010 (Partial) and SR-D.007 (Partial) VERIFIED (KSPEC-003); KSPEC-007 INFERRED entry "Data Protection key-ring loss effects"; docs/DEPLOYMENT.md (key-ring guidance) DOCUMENTED.
- **Impact:** Loss of the key ring can invalidate hashed activation codes, cookies, and protected payloads after an upgrade or container replacement — operational lockout rather than a security breach, but availability-relevant.
- **Proposed correction:** Persist the key ring to a protected, backed-up location (or configurable repository) and cover it in the backup/restore procedure (GAP-034). [PROPOSED]
- **Affected requirements:** SR-S.010, SR-D.007 (Partial).
- **Verification criteria:** T — restart/upgrade preserves Data Protection-protected state.
- **Priority:** High

#### GAP-014 — Weak password policy; no MFA

- **Finding:** Minimum password length is 8 characters with no multifactor authentication option for portal or API administrators.
- **Evidence:** KSPEC-006 §9 cluster 2 and THR-007/THR-009 VERIFIED; docs/user-management.md DOCUMENTED; KSPEC-003 SR-A area (no MFA requirement recorded).
- **Impact:** Administrator credentials are more susceptible to offline and online guessing; NIST SP 800-63B guidance (length-based policy, optional MFA) is not met for an internet-exposed admin surface.
- **Proposed correction:** Raise minimum length, screen against breached-password lists, and add TOTP/WebAuthn MFA for privileged roles. [PROPOSED]
- **Affected requirements:** SR-A.001, SR-A.010; new SR-A/SR-S requirement Proposed.
- **Verification criteria:** T — policy enforced at account creation/change; I — MFA option present.
- **Priority:** Medium

#### GAP-015 — Dev-mode revocation skip and plain-HTTP escape hatches

- **Finding:** Development HTTPS server revocation is explicitly skipped for the launcher's server leaf lacking a CDP; `Kryptonian:Est:AllowPlainHttp=true` can disable the plain-HTTP refusal (426); the sandbox demo requires selecting "skip EST server revocation checks". These exceptions are documented but not technically confined.
- **Evidence:** docs/IMPLEMENTATION-TRACKER.md (boundary: dev leaf without CDP, "not a production recommendation") VERIFIED; docs/medical-device-demo.md (skip option) DOCUMENTED; SR-A.009 "Development bypass confinement" (Existing) VERIFIED (KSPEC-003); `Program.cs` AllowPlainHttp switch CODE-OBSERVED.
- **Impact:** Misconfiguration in a production deployment could silently enable the dev exceptions (plaintext EST, unchecked server revocation).
- **Proposed correction:** Gate escape hatches behind environment assertions (e.g., refuse AllowPlainHttp when not Development; require explicit opt-in file); surface active exceptions in the health/dashboard payload. [PROPOSED]
- **Affected requirements:** SR-A.009, SR-F-EST.012, SR-B.004.
- **Verification criteria:** T — escape hatches rejected in production-mode configuration; I — dashboard displays active exceptions.
- **Priority:** Medium

#### GAP-016 — ACME DNS-01 challenge is a stub

- **Finding:** The ACME connector's DNS-01 challenge is a stub (`Dns01ChallengeStub`); a challenge type advertised but not implemented can fail at issuance rather than at configuration time.
- **Evidence:** KSPEC-004 AS-IS limitation (Dns01ChallengeStub) VERIFIED; SR-F-CAI.007 (Partial) VERIFIED (KSPEC-003); plans/microsoft-acme.md ("Do not silently fall back… replace the stub before DNS-01 is production-ready") DOCUMENTED.
- **Impact:** Misconfigured ACME backends produce late, confusing enrollment failures; silent fallback from HTTP-01 to a stub DNS-01 could issue nothing while appearing configured.
- **Proposed correction:** Implement a real DNS-01 provider abstraction or reject DNS-01 at configuration time with a clear error; add configuration-time challenge-set validation. [PROPOSED]
- **Affected requirements:** SR-F-CAI.007 (Partial).
- **Verification criteria:** T — DNS-01 rejected at configuration time unless a real provider is configured; HTTP-01 flow unchanged.
- **Priority:** Medium

#### GAP-017 — Device demo exports unencrypted PFX/PEM private keys

- **Finding:** The device simulator's export option writes an unencrypted PFX or PEM private key to disk; appropriate only for disposable demo identities.
- **Evidence:** docs/medical-device-demo.md (export warning) DOCUMENTED; SR-F-INT.006 "Demo components carry no clinical claims" (Existing) VERIFIED (KSPEC-003); KSPEC-006 THR-016 (device keys unmanaged boundary) VERIFIED.
- **Impact:** Demo key material may be mishandled and later reused; not a gateway product defect but a shipped-tool hazard.
- **Proposed correction:** Require a passphrase for exports or remove the plaintext PEM option in packaged builds; add explicit warning at export time. [PROPOSED]
- **Affected requirements:** SR-F-INT.006; SR-S.011 (Proposed).
- **Verification criteria:** I — export prompts for passphrase or option removed.
- **Priority:** Low

### 7.3 Interoperability

#### GAP-018 — ADCS/NDES connector protocol gaps

- **Finding:** The ADCS connector (via SCEP/NDES) has five documented protocol gaps versus a real Microsoft NDES: hard-coded SCEP path, challenge-password handling, RA-certificate enrollment, `GetCACaps`/algorithm negotiation (3DES vs AES/SHA-256), and pending/poll (`GetCertInitial`) for manual-approval templates. Backend test also targets a harness-only `/health` route.
- **Evidence:** docs/adcs-real-backend-demo.md §4 Gaps 1–5 VERIFIED (analysis against documented NDES behavior); `src/RTSec.Kryptonian.Infrastructure/Harness/AdcsCaConnector.cs`, `ScepClient.cs` (posts to `scep/adcs?operation=…`) CODE-OBSERVED; SR-F-CAI.005 (Partial) VERIFIED (KSPEC-003).
- **Impact:** Real hospital ADCS enrollment fails or is unexercised until these gaps close; the connector has only been exercised against the in-repo lenient harness.
- **Proposed correction:** Implement Gaps 1–5 (mandatory 1–3) with regression tests against a real NDES lab; make the SCEP path and test mechanism configurable per SR-F-CAI.005. [PROPOSED]
- **Affected requirements:** SR-F-CAI.005 (Partial), SR-F-CAI.002.
- **Verification criteria:** T — Test button passes via `GetCACaps` against real NDES; device enrollment chains to the lab Enterprise CA.
- **Priority:** High

#### GAP-019 — ADCS/EJBCA hospital interoperability (incl. revocation) unverified

- **Finding:** Enrollment and revocation against real hospital CA backends remain unverified: ADCS/NDES offers no revoke operation over SCEP (connector honestly returns false), EJBCA remote revocation requires configured issuer DN and has not been exercised against a real EJBCA, and no hospital deployment evidence exists.
- **Evidence:** docs/IMPLEMENTATION-TRACKER.md ("actual ADCS/EJBCA hospital enrollment/revocation interoperability remains unverified") VERIFIED; docs/CHANGES-AND-ORNET-ALIGNMENT.md §6 DOCUMENTED; SR-F-CAI.006 (Partial) VERIFIED (KSPEC-003); docs/adcs-real-backend-demo.md §6 DOCUMENTED.
- **Impact:** The multi-CA abstraction claim rests on harness and local self-signed evidence only; hospital integration risk (auth, templates, revocation channels) is unquantified.
- **Proposed correction:** Execute the documented ADCS/NDES lab runbook; stand up a real EJBCA and verify enrollment + revocation (REST, or a separate revocation channel for ADCS such as CA Web Services); record executed evidence under artifacts/. [PROPOSED]
- **Affected requirements:** SR-F-CAI.005, SR-F-CAI.006, SR-F-CAI.002.
- **Verification criteria:** D + T — live enrollment/revocation against real ADCS and EJBCA with captured evidence; fail-closed revocation re-tested.
- **Priority:** High

#### GAP-020 — Legacy activation headers retained (compatibility only)

- **Finding:** The sample device client retains pre-standard Kryptonian activation headers; the gateway's standard path is HTTP Basic, and the legacy headers are compatibility-only and must not become an interoperability requirement.
- **Evidence:** docs/IMPLEMENTATION-TRACKER.md (boundaries) VERIFIED; docs/medical-device-demo.md ("current app still uses compatibility activation headers") DOCUMENTED; SR-F-EST.007 (Partial), SR-F-INT.002 (Existing) VERIFIED (KSPEC-003).
- **Impact:** Dual activation paths increase attack/maintenance surface and can confuse integrators about the canonical bootstrap.
- **Proposed correction:** Announce deprecation, migrate the sample client to Basic-only bootstrap, and remove legacy header handling after an agreed window. [PROPOSED]
- **Affected requirements:** SR-F-EST.007, SR-F-INT.002.
- **Verification criteria:** T — sample client enrolls via Basic only; legacy headers rejected after deprecation window (or documented policy).
- **Priority:** Medium

#### GAP-021 — Media-type handling leniency/deviations on EST

- **Finding:** EST media-type handling deviates from strict RFC 7030 profiles: `text/plain` CSR acceptance is an intentional leniency, the repository's OpenAPI contract covers no EST paths, so client-side expectations are not contractually pinned.
- **Evidence:** KSPEC-005 §13 (media-type handling; leniencies noted as deliberate contracts, CODE-OBSERVED) VERIFIED; KSPEC-005 §20 open item 2 (EST paths absent from all OpenAPI files) VERIFIED; SR-F-EST.003, SR-F-EST.013 (Existing) VERIFIED (KSPEC-003).
- **Impact:** Strictly conformant clients and the gateway may disagree on content negotiation; deviations are undocumented in machine-readable contracts.
- **Proposed correction:** Document each leniency as an explicit deviation with rationale in KSPEC-005 §5; align future `/csrattrs` work with RFC 9908 encoding clarifications. [PROPOSED]
- **Affected requirements:** SR-F-EST.003, SR-F-EST.013, SR-F-INT.003.
- **Verification criteria:** I — deviations recorded; T — strict and lenient client both obtain defined outcomes.
- **Priority:** Medium

#### GAP-022 — Joint interoperability test vectors absent

- **Finding:** No joint test-vector matrix with OR.NET (unknown identity, SAN/EKU abuse, spoofed forwarding, rollover, revocation timing, outage) exists; SR-F-INT.008 is Proposed and no independent third-party EST client/server pair has been tested over the real TLS ingress.
- **Evidence:** docs/ORNET-ALIGNMENT-2026-09-09.md (proposed compact executable matrix) DOCUMENTED; SR-F-INT.008 (Proposed) VERIFIED (KSPEC-003); historical evidence includes an independent curl HTTP-Basic EST path and in-repo simulators, but no jointly agreed partner matrix VERIFIED (tracker).
- **Impact:** Interoperability claims remain single-implementation; the ORNET briefing's stated goal of one minimal interoperable test case is unmet.
- **Proposed correction:** Agree one minimal interoperable test case with OR.NET and implement the executable matrix against the real TLS ingress. [PROPOSED]
- **Affected requirements:** SR-F-INT.008 (Proposed), SR-F-INT.001.
- **Verification criteria:** D + T — independent client/server pair executes the matrix; evidence recorded.
- **Priority:** Medium

### 7.4 Architecture

#### GAP-023 — Bounded proxy lifetimes; no instant mid-association revocation

- **Finding:** The DIMSE-TLS proxy enforces trust at handshake with bounded connection lifetimes; revocation does not terminate an in-flight association, and PFX renewal is external (reload demonstrated, CA renewal not automated).
- **Evidence:** docs/IMPLEMENTATION-TRACKER.md (boundaries) VERIFIED; KSPEC-006 THR-010 (bounded proxy lifetimes boundary) VERIFIED; tests/RTSec.Kryptonian.DimseTlsProxy.Tests VERIFIED (tracker evidence).
- **Impact:** Revocation latency for established associations is policy-dependent and undefined for production; operators may expect instant eviction that the design does not provide.
- **Proposed correction:** Document bounded-lifetime policy as the revocation-propagation contract (with SR-C.008 analysis); evaluate connection revalidation or bounded association time as a stakeholder decision. [PROPOSED]
- **Affected requirements:** SR-C.008 (Partial).
- **Verification criteria:** A — propagation contract documented; T — bounded lifetime demonstrated with revoked credential.
- **Priority:** Medium

#### GAP-024 — Admin listener dual-purpose (browser prompt / mTLS coupling)

- **Finding:** Port 7443 optionally accepts client certificates including from browsers, producing installed-certificate-selection prompts in dashboards; an earlier scripted check returned HTTP 200 without a client certificate, so browser-level verification of the prompt behavior was never proven.
- **Evidence:** docs/CHANGES-AND-ORNET-ALIGNMENT.md §6 (dashboard certificate prompt; recommended correction: separate browser admin HTTPS from device mTLS) DOCUMENTED; KSPEC-004 deployment topology (listener separation discussion) DOCUMENTED.
- **Impact:** Usability failure (certificate picker on every dashboard visit) and a blurred trust boundary between admin traffic and device mTLS.
- **Proposed correction:** Separate browser administration HTTPS from device mTLS listeners with browser-level verification of the full workflow. [PROPOSED]
- **Affected requirements:** SR-Z (area); SR-F-ADM.001; SR-B.002.
- **Verification criteria:** D — full browser/device workflow validated on separated listeners; no certificate prompt on dashboard.
- **Priority:** Medium

### 7.5 Reliability

#### GAP-025 — Notification delivery failures silently swallowed

- **Finding:** The notification dispatcher swallows send failures (fire-and-forget with logged-but-otherwise-unobserved errors); failed expiry/revocation/non-renewal notifications produce no operator-visible signal.
- **Evidence:** KSPEC-007 (dispatcher swallow, CODE-OBSERVED; detection scale rationale) VERIFIED; SR-R.003 "Notification delivery decoupled" (Partial), SR-F-CLM.006 (Partial) VERIFIED (KSPEC-003); docs/email-notifications.md DOCUMENTED.
- **Impact:** The renewal-monitoring safety net can be silently absent: operators believe alerts are configured while none are delivered (detection D≥7 in ORK terms).
- **Proposed correction:** Surface delivery outcomes (per-recipient status, retry with backoff, failed-delivery alert/health flag), and record a delivery-status event per notification. [PROPOSED]
- **Affected requirements:** SR-R.003 (Partial), SR-F-CLM.006 (Partial).
- **Verification criteria:** T — forced SMTP failure produces observable delivery-status event and operator-visible indicator.
- **Priority:** High

#### GAP-026 — Renewal runs only while simulator app is open

- **Finding:** Automatic renewal executes only while the `Kryptonian.MedicalDevice` simulator is running (checks once per minute after two-thirds of lifetime); there is no OS-service renewal, no out-of-application mechanism, and no old-certificate retirement policy; expired credentials require attended re-activation.
- **Evidence:** docs/IMPLEMENTATION-TRACKER.md ("Automatic renewal runs only while MedicalDevice is open") VERIFIED; docs/medical-device-demo.md (renewal behavior) DOCUMENTED; KSPEC-007 ORK-001 and TBD-3 VERIFIED; SR-F-CLM.002 (Existing, in-app) VERIFIED (KSPEC-003).
- **Impact:** In the demonstrated operating model, devices that do not run a renewal agent lose credentials at expiry — an availability risk incompatible with unattended clinical-estate operation.
- **Proposed correction:** Define the production renewal model (OS service / scheduled task / gateway-initiated re-enrollment) and early-renewal, retry/backoff, and outage-tolerance policies per the ORNET briefing availability requirements. [PROPOSED]
- **Affected requirements:** SR-F-CLM.002, SR-F-CLM.005, SR-O.003; KSPEC-007 ORK-001.
- **Verification criteria:** D + T — renewal demonstrated with the host application closed under the agreed model.
- **Priority:** High

#### GAP-027 — Clock-synchronization dependency unmitigated

- **Finding:** Certificate validity, activation-code lifetime, renewal windows, and CRL freshness all depend on host clock correctness; the Critical-band clock-skew risk (ORK-009) has no in-repo mitigation (no reliable-time source requirement, skew detection, or alerting).
- **Evidence:** KSPEC-007 ORK-009 (S=8, O=4, D=7, Critical via override) VERIFIED; SR-D.005 UTC discipline (Existing) VERIFIED (KSPEC-003).
- **Impact:** Clock skew can prematurely invalidate valid credentials, extend revocation visibility, or break renewal windows; the SQLite DateTime-kind bug already demonstrated a time-handling failure class.
- **Proposed correction:** Add documented NTP/time-source deployment requirements, skew detection with alerting, and monotonic-time regression tests for revocation arithmetic. [PROPOSED]
- **Affected requirements:** SR-D.005; SR-C.008; SR-R (area).
- **Verification criteria:** T — skewed-clock demonstration with alerts; I — deployment time requirements documented.
- **Priority:** High

### 7.6 Performance

#### GAP-028 — Capacity/latency targets undefined and unmeasured

- **Finding:** Enrollment latency budget (SR-P.003) and production capacity (SR-P.001 thresholds beyond the demonstrated rate-limit configuration) are `TBD`; profile/CA-certificate caching is only partially characterized; no load-testing evidence exists.
- **Evidence:** SR-P.003 (Missing, `TBD`), SR-P.001 (Existing but thresholds unproven at production scale), SR-P.005 (Partial) VERIFIED (KSPEC-003); no performance artifacts under artifacts/ INFERRED.
- **Impact:** Hospital-scale behavior (hundreds of devices renewing concurrently) is unquantified; rate-limit defaults may block legitimate renewal bursts.
- **Proposed correction:** Set stakeholder-agreed capacity/latency targets; run load tests covering renewal bursts; tune caching and rate limits to targets. [PROPOSED]
- **Affected requirements:** SR-P.001, SR-P.003, SR-P.005.
- **Verification criteria:** A + T — documented targets; load-test evidence at target scale.
- **Priority:** Medium

### 7.7 Maintainability

#### GAP-029 — Stale/empty OpenAPI contract files

- **Finding:** `OpenAPI.Client.yaml` is empty (0 bytes) and `OpenAPI.Schema.yaml` is stale relative to DTOs (missing `hostnameMatchType`, `validityDays`, `requireClientCertificate`, trust-policy fields, `isEnabled/isActive`); EST paths appear in no OpenAPI file; several files declare no security scheme.
- **Evidence:** `OpenAPI.Client.yaml`, `OpenAPI.Schema.yaml` (0 bytes; stale) CODE-OBSERVED and recorded in KSPEC-005 §18/§20 VERIFIED; SR-F-INT.003 (Missing), SR-F-INT.004 (Partial) VERIFIED (KSPEC-003 §22).
- **Impact:** Integrators automating against the contracts are silently misled; the "well-documented protocol" positioning (Mvp.md) is not contractually supported.
- **Proposed correction:** Generate authoritative OpenAPI from implemented controllers (or reconcile contract-first files), add an EST endpoint contract, declare security schemes, and add a contract-drift CI check. [PROPOSED]
- **Affected requirements:** SR-F-INT.003 (Missing), SR-F-INT.004 (Partial).
- **Verification criteria:** I — contracts current and complete; T — CI drift check fails on stale contracts.
- **Priority:** High

#### GAP-030 — Unresolved TBD threshold cluster

- **Finding:** Fifteen requirements carry unresolved `TBD` policy values (abuse-control threshold, pending matrix, forwarder mechanism, encryption approach, key archival, certificate lifetime, stale-revocation policy, retention period, capacity, latency budget, delivery reliability, availability targets, log retention, metrics format, connector secrets mechanism) that block closure of their requirements.
- **Evidence:** KSPEC-003 §22 (unresolved thresholds list) VERIFIED; KSPEC-007 TBD table VERIFIED.
- **Impact:** Requirements remain unverifiable; policy decisions are deferred indefinitely; GAP-004/010/011 remediation depends on these decisions.
- **Proposed correction:** Hold a stakeholder decision session to resolve each TBD; record decisions in KSPEC-003/007 and update the affected GAPs. [PROPOSED]
- **Affected requirements:** SR-F-REG.007, SR-F-EST.011, SR-S.005, SR-S.006, SR-S.008, SR-S.011, SR-C.003, SR-C.008, SR-D.006, SR-P.001, SR-P.003, SR-R.003, SR-V.004, SR-O.002, SR-O.004, SR-F-CAI.010.
- **Verification criteria:** I — each TBD resolved with a recorded decision or an explicit accepted-risk entry.
- **Priority:** Medium

#### GAP-031 — No SBOM; dependency audit not whole-system

- **Finding:** No SBOM (CycloneDX/SPDX) is generated for gateway artifacts; the npm-audit reduction and CI gate cover the UI lockfile only, and the tracker explicitly disclaims a whole-system security audit.
- **Evidence:** docs/IMPLEMENTATION-TRACKER.md ("This is not a whole-system audit") VERIFIED; no SBOM tooling in .github/workflows/ CODE-OBSERVED; SR-S.014 (Partial) VERIFIED (KSPEC-003).
- **Impact:** Downstream operators cannot verify shipped component inventory; advisory response lacks an authoritative component list.
- **Proposed correction:** Generate SBOMs (CycloneDX 1.5+ or SPDX 2.3) in CI for server, proxy, and device artifacts; gate on new critical advisories. [PROPOSED]
- **Affected requirements:** SR-S.014 (Partial), SR-O.005.
- **Verification criteria:** T — SBOM produced per artifact in CI; advisory gate demonstrated.
- **Priority:** Medium

### 7.8 Testing

#### GAP-032 — Test-suite skips, analyzer warnings, mocked-vs-live divergence

- **Finding:** Eighteen pre-existing Windows infrastructure test skips and existing analyzer warnings remain; live checks exposed two real bugs (Schannel ephemeral-key rejection, SQLite DateTime-kind CRL shift) that mocked tests missed.
- **Evidence:** docs/IMPLEMENTATION-TRACKER.md (boundaries; "Failed attempts remain alongside successful artifacts") VERIFIED; 8 test projects (corrected per RF-005; KSPEC-000 §10) VERIFIED.
- **Impact:** Coverage is overstated by skip counts; mocked-test pass rates do not establish protocol behavior on real platforms.
- **Proposed correction:** Triage each skip (fix/justify/retire), resolve or baseline analyzer warnings, and promote key mocked tests to live-ingress regressions (GAP-033). [PROPOSED]
- **Affected requirements:** verification methods of all SR-* (KSPEC-008 scope).
- **Verification criteria:** T — skip count reduced with recorded rationale per remaining skip; analyzer warnings resolved or baselined.
- **Priority:** Medium

#### GAP-033 — No independent-client / negative-path matrix over real ingress

- **Finding:** Executed evidence uses in-repo simulators; the briefing's required compact executable matrix (unknown identity, device A requesting B's name, unauthorized SAN/EKU, spoofed forwarding, activation replay, unknown profile, renewal/rekey, revoked/expired, CA outage, issuer rollover, peer denial) has not been run as a single reproducible matrix against the actual TLS ingress.
- **Evidence:** docs/ORNET-ALIGNMENT-2026-09-09.md (joint-slice and matrix requirements) DOCUMENTED; artifacts/developer/verify-*/evidence.json VERIFIED (partial coverage only); SR-F-INT.001 (Existing) VERIFIED (KSPEC-003).
- **Impact:** Negative-path and failure behavior are only partially evidenced; regression risk for security-critical rejections.
- **Proposed correction:** Implement the executable matrix as a repeatable verifier (extending scripts/verify-dev.ps1), including CA-outage and issuer-rollover cases. [PROPOSED]
- **Affected requirements:** SR-F-INT.001; SR-S.003, SR-S.004; SR-X.001–SR-X.005.
- **Verification criteria:** T — matrix runs end-to-end with recorded evidence.json including failure cases.
- **Priority:** Medium

### 7.9 Operational readiness

#### GAP-034 — No backup/restore procedure; upgrade lacks safety boundary

- **Finding:** There is no documented/executed backup-and-restore procedure for database, CA material, or Data Protection keys; MSI rollback is explicitly not a database rollback; schema upgrades run raw additive DDL outside an explicit transaction/backup boundary.
- **Evidence:** SR-X.007 (Partial), SR-D.004 (Missing) VERIFIED (KSPEC-003); KSPEC-007 ORK-006 (recovery: "none beyond reinstallation"; PROPOSED restore procedure) VERIFIED; ORK-011 (Critical, RPN 240) VERIFIED; docker-compose/deploy (no backup tooling) CODE-OBSERVED.
- **Impact:** Irreversible loss of CA and device-trust state is a plausible single-event outcome; upgrade failures (ORK-011 Critical) have no rollback path.
- **Proposed correction:** Implement and rehearse backup/restore (database + CA + key ring) with stakeholder RPO/RTO targets; wrap upgrades in a transaction/backup boundary; add upgrade rehearsal on both persistence providers. [PROPOSED]
- **Affected requirements:** SR-X.007 (Partial), SR-D.004 (Missing); KSPEC-007 ORK-006/ORK-011.
- **Verification criteria:** T — backup/restore drill with defined RPO/RTO passes; upgrade rehearsal on SQLite and PostgreSQL.
- **Priority:** High

#### GAP-035 — Component-level health reporting and metrics missing

- **Finding:** `/api/status/health` covers process liveness only; no component-level health (database reachable, CA backend reachable, CRL freshness, notification channel) and no metrics endpoint exist.
- **Evidence:** SR-V.002 (Missing), SR-O.004 (Missing, format `TBD`) VERIFIED (KSPEC-003); KSPEC-005 StatusController surface VERIFIED.
- **Impact:** Stale CRLs, dead CA backends, and broken notifications (GAP-025) are invisible to monitoring until device enrollments fail.
- **Proposed correction:** Add component checks to the health output and a metrics endpoint (format TBD per SR-O.004); define availability targets (SR-V.004). [PROPOSED]
- **Affected requirements:** SR-V.002, SR-O.004 (Missing); SR-V.004.
- **Verification criteria:** T — induced component failures reflected in health/metrics.
- **Priority:** Medium

#### GAP-036 — PostgreSQL provider never exercised

- **Finding:** PostgreSQL schema upgrades exist and the Azure deployment plan uses PostgreSQL, but all executed persistence evidence (restart preservation, upgrade behavior, revocation arithmetic) is SQLite-only.
- **Evidence:** docs/IMPLEMENTATION-TRACKER.md ("PostgreSQL schema upgrades are implemented but only SQLite persistence was exercised") VERIFIED; docs/CHANGES-AND-ORNET-ALIGNMENT.md §6 DOCUMENTED; deploy/azure/README.md (PostgreSQL Flexible Server) DOCUMENTED; SR-D.002 (Partial) VERIFIED (KSPEC-003).
- **Impact:** Provider-specific behavior (timestamp handling, DDL, connection failures) is untested on the provider chosen for the cloud deployment path.
- **Proposed correction:** Exercise the full verification suite (or an equivalent subset) on PostgreSQL 16, including upgrade and restore, and record evidence. [PROPOSED]
- **Affected requirements:** SR-D.002 (Partial), SR-D.003.
- **Verification criteria:** T — executed PostgreSQL test evidence under artifacts/; upgrade+restore drill passes.
- **Priority:** High

#### GAP-037 — Deployment posture defaults (loopback, unsigned installer, demo records)

- **Finding:** The Windows gateway package listens only on loopback by default, ships an unsigned lab-preview device executable, retains synthetic demo records/user artifacts, and carries documentation-not-implementation disclaimers; appropriate for lab scope but not deployment-hardening defaults.
- **Evidence:** docs/medical-device-demo.md (unsigned lab preview; loopback-only) DOCUMENTED; docs/IMPLEMENTATION-TRACKER.md ("Existing user artifacts and services were preserved. Synthetic demo records remain.") VERIFIED; README.md disclaimer VERIFIED; SR-B area (KSPEC-003) VERIFIED.
- **Impact:** Moving the package to a hospital network without hardening (code signing, external listener configuration, demo-data purge, independent security assessment) silently inherits prototype assumptions.
- **Proposed correction:** Produce a production-readiness checklist (code signing, listener/firewall configuration, demo-data purge, independent security assessment per the README disclaimer) and gate release artifacts on it. [PROPOSED]
- **Affected requirements:** SR-B.001–SR-B.004, SR-B.007 (Partial).
- **Verification criteria:** I — checklist approved and applied to release artifacts; D — hardened install exercised.
- **Priority:** Medium

---

## 8. Prioritized remediation ordering

Ordering rationale: deployment blockers and gateway-wide exposures first, then
lifecycle-guarantee and interoperability unverifieds, then tracked improvements.
Every item is PROPOSED until the corresponding change and its verification evidence
are reviewed and approved (KSPEC-000 §4, §13).

### Wave 0 — Deployment blockers (must close or be risk-accepted before any non-lab deployment)

1. **GAP-005** (dependency advisories): tested framework/native-provider update; .NET-wide CI advisory gate.
2. **GAP-006** (JWT fallback secret): fail-closed startup; remove fallback constant.
3. **GAP-007** (admin auth lockout/rate limiting): throttle/lock `/api/auth/*`; password policy (feeds GAP-014).

### Wave 1 — Core guarantee and interoperability unverifieds (before production planning)

4. **GAP-019** (hospital CA interoperability): real ADCS/NDES and EJBCA enrollment+revocation evidence.
5. **GAP-018** (ADCS connector gaps 1–5) — prerequisite for GAP-019.
6. **GAP-034** (backup/restore + upgrade boundary) with GAP-013 (key-ring persistence).
7. **GAP-011** (secrets at rest / vault) and **GAP-036** (PostgreSQL exercise).
8. **GAP-025** (notification delivery visibility), **GAP-026** (out-of-application renewal model), **GAP-027** (time-source requirements).
9. **GAP-009** (authenticated forwarder), **GAP-008** (legacy static key deprecation), **GAP-010** (CRL lifetime policy), **GAP-012** (audit tamper-evidence).
10. **GAP-029** (OpenAPI currency) and **GAP-030** (TBD decision session — prerequisite for GAP-004/010/011 closure).

### Wave 2 — Tracked improvements

11. **GAP-001** (`/csrattrs`, RFC 9908-aligned), **GAP-004** (pending matrix — depends on GAP-030).
12. **GAP-031** (SBOM), **GAP-032** (skip/analyzer triage), **GAP-033** (executable negative-path matrix), **GAP-022** (joint test vectors), **GAP-021** (media-type deviation record).
13. **GAP-015** (escape-hatch confinement), **GAP-016** (DNS-01 stub), **GAP-023** (revocation-propagation contract), **GAP-024** (listener separation), **GAP-028** (capacity/latency targets), **GAP-035** (component health/metrics), **GAP-037** (production-readiness checklist).

### Wave 3 — Lab-scope items with recorded rationale

14. **GAP-002** (`/serverkeygen` scope decision), **GAP-003** (`/fullcmc` deviation record), **GAP-014** (password/MFA — partially pulled into Wave 0 via GAP-007), **GAP-017** (demo key export).

Dependencies: GAP-030 blocks GAP-004, GAP-010, GAP-011 closure; GAP-018 blocks
GAP-019; GAP-034 absorbs GAP-013 verification; GAP-037 depends on Waves 0–1.

## 9. Traceability notes

- Every gap cites its evidence with a provenance class (KSPEC-000 §5); no claim in
  this document upgrades DOCUMENTED evidence to VERIFIED.
- Affected-requirements IDs reference the canonical KSPEC-003 register (§21.2) only;
  this document defines no SR/UR IDs.
- Cross-references: KSPEC-005 §20 (interface open items), KSPEC-006 §9 (residual-risk
  clusters), KSPEC-007 §TBD (operational open decisions), KSPEC-008 (verification
  planning consumes GAP verification criteria), KSPEC-009 (traceability), and
  KSPEC-011 (standards-focused readiness analysis, which consumes the protocol and
  control gaps, GAP-001–GAP-022).
- All 40 gaps carry approval state **Review Required**; no approval is recorded or
  inferred.

## 10. Open questions for review

1. Are the Wave assignments acceptable to stakeholders as a deployment-gating order?
2. Should `/fullcmc` and `/serverkeygen` be declared out of scope permanently (GAP-002/003) or planned?
3. Which stakeholder body resolves the TBD cluster (GAP-030), and by what date?
4. Does the audit tamper-evidence approach (GAP-012) target ATNA-compatible export now or later?
5. Is PostgreSQL the sole production provider going forward (GAP-036), or must SQLite hardening continue in parallel?

---

*End of KSPEC-010, version 0.2 (Draft). All findings carry Priority assignments by the
draft authors and Approval "Review Required"; no approval is recorded or inferred.*
## 11. Additional gaps found during code/document reconciliation

#### GAP-038 — Incomplete durable lifecycle audit

- **Finding:** Some enrollment/renewal failures return before EnrollmentEvent persistence, while controller failures and revocation decisions are not uniformly recorded as durable lifecycle events.
- **Evidence:** `EnrollmentOrchestrator.EnrollAsync` and `ReenrollAsync`; `EstController`; `CertificateRevocationService.RevokeAsync` (CODE-OBSERVED).
- **Impact:** Operators cannot reconstruct every credential/trust decision from the enrollment event store. Logging and certificate status alone do not satisfy the target audit requirement.
- **Proposed correction:** Introduce complete, secret-free durable decision auditing at the protocol/service boundary with stable correlation IDs, preserving failure status and transaction semantics. [PROPOSED]
- **Affected requirements:** SR-C.010, SR-U.001, SR-U.002 (Partial).
- **Verification criteria:** VT-059, including early rejection and revocation cases, persistence and secret exclusion.
- **Priority:** High

#### GAP-039 — Activation consumption precedes issuance and recovery is unverified

- **Finding:** Activation consumption is committed before backend resolution/policy/issuance. CA failures, pending issuance and process termination can leave a Pending device with a consumed code; retrying that bootstrap cannot be presumed safe.
- **Evidence:** `EnrollmentOrchestrator.EnrollAsync`, `TryConsumeActivationCodeAsync`, `IssueForDeviceAsync`; `UnitOfWork.TryConsumeActivationCodeAsync` (CODE-OBSERVED).
- **Impact:** Bootstrap fails after consuming its one-time credential and may require administrator code regeneration. Client handling of HTTP 202 alone does not prove initial enrollment recovery.
- **Proposed correction:** Specify and demonstrate supported regeneration/recovery; decide on any issuance idempotency or durable pending-workflow design before implementation. [PROPOSED]
- **Affected requirements:** SR-F-REG.002, SR-F-EST.010/.011, SR-R.004.
- **Verification criteria:** VT-060 with persisted state, restart, CA outage/pending and concurrency.
- **Priority:** High

#### GAP-040 — Forwarded source address is not authenticated

- **Finding:** `EstController.GetClientIp` takes the first X-Forwarded-For value, and rate limiting selects the same header via RealIpHeader. CertificateForwardingGuard protects XFCC, not the authenticity of this source-address header.
- **Evidence:** `EstController.GetClientIp`; `IpRateLimiting.RealIpHeader` in appsettings.json; `Program.cs` middleware; `CertificateForwardingGuard` (CODE-OBSERVED).
- **Impact:** An untrusted caller may influence audit source attribution and the per-IP throttle key. Source consistency between logging and limiting is not proof of trusted origin.
- **Proposed correction:** Define trusted proxy address handling and remove/ignore untrusted forwarded headers before both logging and rate limiting. [PROPOSED]
- **Affected requirements:** SR-S.005/.006, SR-P.001, SR-C.010, SR-U.001.
- **Verification criteria:** Extend VT-055/VT-059 with direct-header spoofing and authenticated-forwarder cases; direct callers cannot select arbitrary origin addresses or bypass throttling.
- **Priority:** High
