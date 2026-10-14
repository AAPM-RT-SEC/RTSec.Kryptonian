# Kryptonian Formal Engineering Specification — Security and Standards Conformance Assessment

| Field | Value |
|---|---|
| Document title | Security and Standards Conformance Assessment |
| Document ID | KSPEC-011 |
| Version | 0.2 (Draft) |
| Review status | Draft — pending review |
| Created | 2026-10-13 |
| Baseline repository commit | `682f996c8447afec13bc1fce5b7f59e6a06efa25` (branch `feat/windows-sandbox-installer`, "Release Windows gateway and portable enrollment demo 0.1.4") |
| Repository | https://github.com/AAPM-RT-SEC/RTSec.Kryptonian |
| Revision history | 0.1 — 2026-10-13 — Initial draft: standards applicability table, per-protocol capability status, interoperability evidence summary, security-control coverage mapping (Author: conformance assessment team). |
| Source references | KSPEC-000 (conventions); KSPEC-003 (SR register); KSPEC-005 (ICD); KSPEC-006 (cybersecurity); KSPEC-010 (gap analysis, GAP-001–GAP-037); docs/IMPLEMENTATION-TRACKER.md; docs/ORNET-ALIGNMENT-2026-09-09.md; docs/CHANGES-AND-ORNET-ALIGNMENT.md; docs/medical-device-demo.md; docs/adcs-real-backend-demo.md; artifacts/developer/verify-*/evidence.json |
| Author | Name: TBD — Date: TBD — Signature: TBD |
| Reviewer | Name: TBD — Date: TBD — Signature: TBD |
| Approver | Name: TBD — Date: TBD — Signature: TBD |
| Reconciliation revision | 0.2 - 2026-10-13 - source/document audit and publication controls (KSPEC-016) |

**Product classification (binding, KSPEC-000 §3).** Kryptonian is enterprise
healthcare IT cybersecurity infrastructure: an EST (RFC 7030) certificate enrollment,
identity provisioning, and credential lifecycle gateway. It is **not a regulated
medical device**, performs no clinical functions, and this document contains no
regulatory, IEC 62304, ISO 14971, or medical-device content.

---

## 1. Purpose, scope, and mandatory disclaimer

This document (KSPEC-011) is a **readiness analysis**: it maps the capabilities
present in the baseline implementation to the protocols and standards that the
repository's own documentation cites (RFC 7030 and updates, X.509/PKIX, TLS,
DICOM TLS profiles, IEEE 11073 SDC / OR.NET context, IHE SDPi context, and security
control frameworks), and records what is supported, where behavior deviates, and
what remains untested.

**Provenance and claim discipline (KSPEC-000 §5, §13).** Throughout this document:

- **Supported** means the capability is implemented and there is positive executed
  or source-inspection evidence.
- **Deviating** means behavior differs from the cited text in a way recorded in
  KSPEC-005 or docs/IMPLEMENTATION-TRACKER.md.
- **Untested** means the requirement cannot be evidenced from the baseline
  repository; its status is UNKNOWN until exercised.

**Disclaimer (binding).** No third-party certification, accreditation, or
standards-body approval is claimed or implied for any protocol, profile, or
framework cited here. Statements of the form "aligned with", "consistent with", or
"compatible with" describe the repository's own implementation choices and
evidence, not a conformity determination. No OR.NET adoption, IHE SDPi integration
approval, DICOM TLS certification, SDC conformity, or NIST validation is claimed.
The README disclaimer (independent security assessment required; research
prototype) remains operative.

## 2. Standards version notes

Versions are recorded as referenced during drafting (2026-10-13). Where an edition
could not be independently re-verified during drafting, that is stated rather than
guessed.

### 2.1 Applicable protocols and standards table

| Protocol / standard | Version cited | Applicability to Kryptonian components | Applicability class |
|---|---|---|---|
| RFC 7030 (EST) | 2014 | Core device-facing enrollment protocol: `cacerts`, `simpleenroll`, `simplereenroll`, HTTP Basic bootstrap, 202-pending handling | **Mandatory** (defining protocol of the product) |
| RFC 7929 (TLS-based authentication methods for EST) | 2016 | Alternative EST authentication paths (TLS-only bootstrap); not implemented — context for future auth-method decisions | Recommended (context) |
| RFC 8295 (EST algorithms with Internet PKI) | 2017 | Algorithm requirements for EST CSRs/certificates; profile enforcement aligns informally (CSR algorithm floor) | Recommended |
| RFC 9908 (CSR attributes clarification) | Jan 2026 | Governs `/csrattrs` encoding if/when implemented (GAP-001) | Optional now; recommended before implementing `/csrattrs` |
| RFC 5280 (X.509 PKIX profile) | 2008 | Certificate and CRL profile features used by issuers, validation, and the local CA | **Mandatory** (basis of all issuance/validation) |
| RFC 6960 (OCSP) | 2013 | Not implemented; CRL-based revocation only | Optional |
| RFC 5751/CMS (PKCS#7), RFC 2986 (PKCS#10) | current | CSR input and PKCS#7 certificate-chain responses | Mandatory (implied by EST) |
| RFC 8555 (ACME) | 2019 | Southbound CA connector (HTTP-01 implemented; DNS-01 stub — GAP-016) | Optional backend (recommended for cloud CAs) |
| ISO/IEC/IEEE 29148:2018 | 2018 | Requirement/vocabulary conventions for this specification package (KSPEC-000) | Mandatory for the docs, not the software |
| ISO/IEC 27001:2022 / 27002:2022 | 2022 | Recommended-practice context for operational controls; no ISMS/conformity claimed | Recommended (context) |
| NIST SP 800-53 Rev. 5 | 2020 | Control catalogue for §8 coverage mapping; recommended practices only | Recommended (context) |
| NIST SP 800-63B (Rev. 3 2017; Rev. 4 published 2025) | see note | Password/authenticator guidance for admin authentication; Rev. 4 existence verified 2026-10-13 via pages.nist.gov fetch; formal mapping not performed | Recommended |
| NIST SP 800-57 Pt 1 Rev. 5 | 2020 | Key-management guidance (CA key protection, lifetimes) — partly unmet (file-based CA keys, THR-016) | Recommended |
| NIST SP 800-52 Rev. 2 | 2019 | TLS protocol/cipher guidance for the gateway's TLS listeners | Recommended |
| NIST SP 800-218 v1.1 | 2024 | Secure-development practices (CI, review, dependency policy) | Recommended |
| DICOM PS3.15 (B.12/B.13 TLS profiles) | current | Related context for the DIMSE-TLS demonstration; **not certified, not claimed** | Optional / related context |
| DICOM PS3.18 | current | Context for the DICOM-over-TLS application path | Optional / related context |
| IEEE 11073-20701 / -20702 (SDC) | 2018-era | **Research goals only** via OR.NET collaboration; no adopted profile; full texts unavailable to review | Informational |
| IHE SDPi Technical Framework | 2.4 (Trial Implementation / Standard for Trial Use, 7 May 2026) | Context only; no EST enrollment profile adopted by OR.NET | Informational |
| IHE ATNA | current | Future audit-format mapping (GAP-012); not implemented | Optional / future |
| OWASP ASVS | 4.0.3 | Verification checklist for §11 mapping | Recommended |
| CycloneDX 1.5+ / SPDX 2.3 | 1.5+ / 2.3 | Candidate SBOM formats (GAP-031); not implemented | Optional |

Applicability classes: **Mandatory** = the product's defined behavior depends on it;
Recommended = good-practice reference, not adopted as a conformance target;
Optional = selective/conditional use; Informational = context only.

(RFC 8951, a base64-processing clarification for EST referenced by
docs/ORNET-ALIGNMENT-2026-09-09.md, is carried as DOCUMENTED-only context; its
edition was not re-verified during drafting.)

## 3. RFC 7030 capability status

Status vocabulary per §1; evidence classes per KSPEC-000 §5.

| RFC 7030 capability (clause) | Status | Evidence and notes |
|---|---|---|
| `/cacerts` — CA certificate chain distribution (§4.1) | Supported | `EstController.cs`; executed curl-based evidence VERIFIED (tracker). Profile resolved by host/path; unknown label → 404 with no default fallback (deliberate hardening) |
| `simpleenroll` (§4.2.1) | Supported | PKCS#10 in, PKCS#7 chain out; 64 KiB CSR cap; executed Schannel mTLS enrollment evidence VERIFIED |
| `simplereenroll` (§4.2.2) | Supported | Requires native TLS client certificate; new key generated, approved identity preserved — executed evidence VERIFIED |
| HTTP Basic bootstrap authentication (§3.2.3) | Supported (credential-semantics choice) | Standard Basic over HTTPS using registered device UUID + one-time activation code (PR #42); activation-code semantics are a documented gateway choice |
| Re-enroll authentication over TLS (§3.2.3) | Supported | Native TLS client certificate required; 401 without — executed evidence VERIFIED |
| Pending enrollment (202 semantics) | Supported (Retry-After defined; trigger matrix undefined) | 202 + `Retry-After` + JSON body CODE-OBSERVED; per-backend pending triggers `TBD` (GAP-004, SR-F-EST.011 Missing) |
| `/csrattrs` (§4.5.2) | **Not implemented** | GAP-001 (SR-F-EST.014 Missing); RFC 9908 (verified, Jan 2026) clarifies encoding for any future implementation |
| `/serverkeygen` (§4.5.3) | **Not implemented** | GAP-002; stakeholder scope decision open |
| Full CMC (`fullcmc`, §4.6) | **Not implemented** | GAP-003; declared subset scope (Mvp.md); recorded as deviation |
| Media types (§4.2.3) | Deviating (documented leniency) | `text/plain` CSR acceptance intentional; strict media-type interop remains untested (KSPEC-005 §13) — GAP-021 |
| DNS-based discovery (§3.1) | Untested | `ESTserver.<domain>` TXT discovery not exercised; clients are preconfigured with enrollment URLs (briefing guidance) |
| Link-header rel="next" / additional endpoints (§4.5) | Untested | Not implemented and not exercised |
| Legacy private activation headers | Deviating (compatibility only) | Sample-client compatibility only; must not become an interoperability requirement (SR-F-EST.007/SR-F-INT.002; GAP-020) |
| CRL inside `/cacerts` (§4.1 informational) | Untested | CRLs are published from a separate CRL controller path, not embedded in `/cacerts` |

## 4. X.509 / PKIX (RFC 5280) profile features

| RFC 5280 feature | Status | Evidence and notes |
|---|---|---|
| Subject DN / issuer DN | Supported | Profile subject-DN rules enforced at issuance (SR-C.001); issuer matching on re-enrollment (409 issuer-mismatch guard, SR-X.003) — VERIFIED |
| Subject Alternative Name | Supported (policy-gated) | SAN handling bound by profile; unauthorized SAN assertions in the proposed test matrix (GAP-033) are unexecuted — CODE-OBSERVED for enforcement, negative-path evidence partial |
| Extended Key Usage (clientAuth 1.3.6.1.5.5.7.3.2 / serverAuth) | Supported | Client certificates validated for clientAuth EKU; profile key-usage/EKU policy enforced (SR-C.001, SR-S.002) — executed + CODE-OBSERVED |
| Key usage / basic constraints | Supported | Profile key-usage policy; leaf-vs-CA checks in `ConfiguredClientCertificateTrust` — CODE-OBSERVED |
| CRLs (issuer-scoped, signed, CRL number, nextUpdate) | Supported (prototype cadence) | Issuer-scoped signed CRLs with monotonic CRL number and `nextUpdate`; transactional revocation persistence (SR-F-CLM.007/008) — executed evidence VERIFIED; 60-second lifetime is a demo value (GAP-010) |
| CRL distribution points on issued certificates | Supported | SR-C.006 — CODE-OBSERVED; launcher server leaf intentionally lacks a CDP (documented dev exception) |
| Online revocation checking (X509Chain + CRL retrieval) | Supported (demonstrated) | Native online X509Chain reports Revoked independently of registry lookup — executed evidence VERIFIED (tracker); 5-second URL timeout |
| OCSP (RFC 6960) | Not implemented | Optional; CRL-only revocation at baseline |
| AIA (authority information access) | Untested | Not evidenced in issued certificates or validation path — UNKNOWN |
| Path validation (RFC 5280 §6) via custom root trust | Supported (implementation-selected) | Custom-root-trust chain build for client certificates and proxy peers — executed evidence VERIFIED; full RFC 5280 §6 algorithm conformance not separately verified |
| Name constraints / policy extensions | Not used | Not required by profiles; noted as not exercised |
| SHA-256/RSA and ECDSA algorithm support | Partially evidenced | CSR algorithm floor enforced (SR-S.002 area); RFC 8295 algorithm-set alignment not formally checked |

## 5. TLS posture (NIST SP 800-52 Rev. 2 as guidance)

| Aspect | Status | Notes |
|---|---|---|
| TLS 1.2 minimum | Supported (default posture) | ASP.NET Core/Kestrel defaults per deployment configuration; explicit min-version pinning not evidenced in-repo — CODE-OBSERVED defaults, explicit SP 800-52 mapping not performed |
| TLS 1.3 availability | Untested (platform-provided) | Available via .NET/Schannel or OpenSSL depending on host; no explicit configuration or test evidence |
| Cipher-suite policy per SP 800-52 Rev. 2 | Untested | No in-repo cipher configuration; SP 800-52 Appendix suites not mapped |
| mTLS (client certificates) on device listeners | Supported | `ClientCertificateMode.AllowCertificate` + `ConfiguredClientCertificateTrust`; executed mTLS evidence VERIFIED |
| mTLS on DICOM demonstration peers | Supported (demo) | Both peers enforce issuer, TLS purpose, and online revocation — executed evidence VERIFIED |
| Plain-HTTP refusal (426) for EST | Supported | Executed + CODE-OBSERVED; `AllowPlainHttp` escape hatch documented (GAP-015) |
| Certificate-forwarded identity over TLS (XFCC) | Deviating | Proxy-trust boundary; forwarder authenticated by source IP only (GAP-009, SR-S.005) |
| Forward secrecy / key-exchange review | Untested | No evidence; deferred to independent security assessment (README disclaimer) |
| HSTS / TLS hardening on admin listener | Untested | Not evidenced; belongs to hardening checklist (GAP-037) |

## 6. DICOM TLS profiles (B.12 / B.13) — related context, explicitly no certification claim

| Aspect | Status | Notes |
|---|---|---|
| Mutual-TLS DICOM association (DIMSE over TLS) | Demonstrated (lab) | Two actual mutual-TLS DICOM C-STORE transfers with received-file reopen and SOP Instance UID comparison — executed evidence VERIFIED (tracker, artifacts/developer evidence.json) |
| TLS cipher-suite selection per DICOM B.12/B.13 profiles | Untested | No profile-specific suite negotiation was exercised or configured; fo-dicom TLS store defaults used |
| Peer certificate authorization (issuer, purpose, revocation) | Supported (demonstrated) | Both peers enforce issuer, TLS purpose, and online revocation — executed evidence VERIFIED |
| B.12/B.13 certification | **Not claimed** | Per KSPEC-000 §3 and docs/IMPLEMENTATION-TRACKER.md: "No … DICOM B.12/B.13 certification is claimed." The DICOM TLS profile documents define TLS behavior for DICOM; Kryptonian's demonstration is consistent in spirit (mTLS DICOM transport with gateway-governed trust) but no profile conformance testing was performed |
| Role selection / SCU-SCP TLS requirements of B.13 | Untested | Not evaluated against the profile text |

**Related-context boundary (KSPEC-000 §3):** the DIMSE-TLS proxy is a demonstration
component; it establishes network trust (mTLS peer authentication) for DICOM
associations and derives no clinical capability or DICOM conformance.

## 7. IEEE 11073 SDC / OR.NET alignment — research goals only

| Aspect | Status | Notes |
|---|---|---|
| SDC (IEEE 11073-20701/-20702) participation | **Not claimed** | No SDC participant interoperability test has been performed; no SDC conformity is claimed (docs/CHANGES-AND-ORNET-ALIGNMENT.md) |
| OR.NET certificate working-group alignment | Proposed collaboration | EST direction and white paper are correspondence-reported intentions; not an agreed joint architecture (docs/ORNET-ALIGNMENT-2026-09-09.md) |
| Adopted EST profile for medical devices | None | No OR.NET-approved clinical identity/role profile exists; Kryptonian implements its own profile policy (KSPEC-003 SR-C.001) |
| SDC OIDs in issued certificates | Not used | "Issuing a certificate containing SDC OIDs alone is not an SDC interoperability demonstration" (briefing) |
| IHE SDPi | Context only | SDPi 2.4 is Trial Implementation; its existence does not establish that Kryptonian's bootstrap, certificate contents, or renewal policy are accepted |
| Certificate renewal semantics | Cryptographic credential renewal only | Never recertifies clinical safety, regulatory status, or SDC conformity (binding wording across docs) |

## 8. Supported capabilities vs known deviations vs untested requirements

| Capability | Classification |
|---|---|
| EST cacerts/simpleenroll/simplereenroll with Basic bootstrap and native-TLS re-enroll | Supported (executed evidence) |
| One-time atomic activation; replay rejection | Supported (executed evidence) |
| Approval-gated enrollment; profile policy enforcement (subject DN, EKU, key usage, validity) | Supported (executed + CODE-OBSERVED) |
| Issuer-scoped signed CRLs; transactional revocation; fail-closed on unsupported remote revocation | Supported (executed evidence) |
| Automatic renewal with new key and identity preservation | Supported (executed evidence; in-application only — GAP-026) |
| Restart state preservation on SQLite | Supported (executed evidence) |
| DIMSE-TLS proxy trust checks (unknown/expired/revoked rejection; atomic PFX reload) | Supported (executed evidence) |
| Legacy compatibility activation headers | Deviating (compatibility only; GAP-020) |
| XFCC forwarded identity (source-IP-trusted proxy) | Deviating (GAP-009) |
| Media-type leniencies (text/plain CSR acceptance) | Deviating (documented; GAP-021) |
| 60-second prototype CRL lifetime | Deviating (demo value; GAP-010) |
| Developer server-leaf revocation skip (no CDP) | Deviating (dev-only; GAP-015) |
| `/csrattrs`, `/serverkeygen`, `/fullcmc`, DNS-based discovery, Link headers, OCSP | Untested / not implemented (GAP-001/002/003) |
| EST algorithm-set conformance to RFC 8295; RFC 5280 §6 full path-validation audit | Untested |
| SP 800-52 cipher policy; TLS 1.3 configuration | Untested |
| ATNA/DICOM audit event mapping; audit export | Untested / not implemented (GAP-012) |
| Hospital ADCS/EJBCA enrollment + revocation | Untested (GAP-018/019) |
| SDC participant exchange; OR.NET joint test vectors | Untested (GAP-022) |

## 9. Interoperability evidence summary (what was actually demonstrated)

Per docs/IMPLEMENTATION-TRACKER.md executed evidence and artifacts/developer/
verify-*/evidence.json; nothing below is upgraded beyond its recorded provenance.

| Evidence item | Demonstrated | Provenance |
|---|---|---|
| Independent curl HTTP Basic EST over real HTTPS (no private headers) | Initial enrollment path with a generic client | VERIFIED (tracker) |
| One-time activation replay rejection; renewal with new key preserving approved identity | Lifecycle core semantics | VERIFIED (tracker) |
| Forwarded-certificate spoof rejection; HTTP listener denies non-CRL requests | Negative-path XFCC/CRL listener controls | VERIFIED (tracker) |
| Two mutual-TLS DICOM C-STORE transfers with SOP Instance UID comparison; both peers enforce issuer/TLS purpose/online revocation | DICOM-over-mTLS demonstration | VERIFIED (tracker) |
| Administrator revocation → native X509Chain reports Revoked (no cache flush) | Revocation propagation through local CA CRLs | VERIFIED (tracker) |
| Gateway restart on populated SQLite preserves profile/issuer/devices | Restart state preservation (SQLite only) | VERIFIED (tracker) |
| Five loopback proxy tests incl. unknown/expired/revoked rejection and atomic PFX replacement (expired replacement fails closed) | Proxy trust lifecycle | VERIFIED (tracker) |
| Twenty device enrollment/lifecycle tests incl. real Schannel mTLS; WPF build; developer trust regression | Device-side client behavior | VERIFIED (tracker) |
| Windows CI builds UI + solution + proxy; UI npm advisory gate | Build/dependency gates (UI scope only) | VERIFIED (tracker) |
| ADCS connector vs real Microsoft NDES | **Not demonstrated** (runbook only; five connector gaps documented) | DOCUMENTED (docs/adcs-real-backend-demo.md) |
| EJBCA REST enrollment/revocation vs a real EJBCA | **Not demonstrated** | DOCUMENTED (SR-F-CAI.006) |
| Independent third-party EST server/client pair; issuer rollover; CA outage | **Not demonstrated** | DOCUMENTED (briefing matrix) |
| SDC participant exchange; OR.NET joint testing | **Not demonstrated** | DOCUMENTED (boundaries) |
| PostgreSQL persistence | **Not demonstrated** | DOCUMENTED (tracker) |

## 10. Required additional verification

1. Hospital CA interop: execute the ADCS/NDES runbook (GAP-018) and a real-EJBCA
   enrollment + revocation exercise (GAP-019); record artifacts.
2. Independent-client EST matrix over real TLS ingress, including failure paths
   (GAP-033) and one jointly agreed OR.NET test case (GAP-022).
3. TLS policy review against SP 800-52 Rev. 2 (min version, suites, forward secrecy)
   and DICOM B.12/B.13 suite selection if profile alignment is ever desired (§5, §6).
4. RFC 7030 completeness: `/csrattrs` per RFC 9908; pending-trigger matrix; media-type
   deviation record (GAP-001/004/021).
5. RFC 5280 §6 path-validation audit and RFC 8295 algorithm-set check (§4).
6. Security assessment: independent assessment per README disclaimer; ASVS-based
   review (§11 mapping); SP 800-63B-based authentication review; SP 800-218 SDV
   practices check; SP 800-57 key-management review for CA keys.
7. PostgreSQL verification and backup/restore drills (GAP-034/036).
8. ATNA/DICOM audit-mapping analysis if audit interop becomes a goal (GAP-012).

## 11. Security control coverage mapping

Recommended-practice mapping only (KSPEC-000 §13): no conformity claim is made;
rows marked **Gap** correspond to KSPEC-010 GAP IDs.

### 11.1 NIST SP 800-53 Rev. 5 — selected families

| Family | Implemented controls (AS-IS, with provenance) | Gaps |
|---|---|---|
| AC (Access Control) | Three-role RBAC; explicit role policies on mutations (partial); anonymous-surface limitation; CORS restriction (SR-Z, SR-S.013) — VERIFIED/CODE-OBSERVED | SR-Z.007 partial (mutation role-policy completeness); admin rate limiting absent (GAP-007) |
| AU (Audit) | Enrollment events, blocked attempts, admin action logging, structured correlated logging (SR-U.001–.004) — VERIFIED/CODE-OBSERVED | No tamper-evidence, no export (GAP-012) |
| CA (Assessment) | Threat model (KSPEC-006), risk assessment (KSPEC-007), CI checks — draft artifacts | No independent assessment (README disclaimer; GAP-037) |
| CM (Configuration) | Documented configuration settings; additive schema upgrades — CODE-OBSERVED | Upgrade boundary, config-drift detection (GAP-034, GAP-029) |
| IA (Identification/Authentication) | Dual-scheme admin auth; BCrypt wf-12; hashed API keys; activation-code authentication — VERIFIED | JWT fallback secret (GAP-006), lockout (GAP-007), static keys (GAP-008), password policy/MFA (GAP-014) |
| IR (Incident Response) | Failed revocation reported honestly; fail-closed behaviors (SR-X.001/.002) — CODE-OBSERVED | No incident-response procedure/runbook — new requirement Proposed |
| PS (Personnel) | Not addressed (out of scope for a lab reference implementation) | Organizational controls — not applicable at baseline |
| SC (System/Communications) | HTTPS-enforced EST (426), TLS-only CRL listener restriction, mTLS, configured EST rate limits and CORS — CODE-OBSERVED; API CSRF protection not established by this review | SP 800-52 cipher policy (GAP-015/§5); XFCC forwarder auth (GAP-009) |
| SI (System Integrity) | CSR/returned-cert policy enforcement; certificate re-validation; fail-closed revocation; dependency advisories tracked — VERIFIED | Unremediated high advisories (GAP-005); no SBOM (GAP-031) |
| CP (Contingency) | Restart state preservation — VERIFIED (SQLite) | No backup/restore; no availability targets (GAP-034, GAP-035) |

### 11.2 OWASP ASVS 4.0.3 — chapter-level coverage

| ASVS chapter | Coverage at baseline | Gaps |
|---|---|---|
| V1 Architecture/threat | STRIDE model (KSPEC-006); trust-boundary diagram (KSPEC-004) | XFCC proxy trust boundary (GAP-009) |
| V2 Authentication | BCrypt wf-12; hashed API keys; JWT validation; login-failure non-enumeration (SR-A) | JWT secret fallback (GAP-006); lockout/rate limit (GAP-007); MFA absent (GAP-014) |
| V3 Session | 8-hour JWT lifetime; API-key role binding — VERIFIED/CODE-OBSERVED | Session revocation on credential change not evidenced — open question |
| V4 Access control | Three-role RBAC; role policies (SR-Z) | SR-Z.007 partial mutation coverage; self-service model Proposed (SR-Z.009) |
| V5 Validation/encoding | CSR parsing bounds (64 KiB); policy validation of CA responses | Framework-level; no dedicated review performed — untested |
| V6 Cryptography | BCrypt; SHA-256 thumbprints; RSA/EC issuance; Data Protection | Secrets at rest (GAP-011); key-ring persistence (GAP-013); CA keys file-based (SP 800-57 gap) |
| V7 Error handling/logging | Structured correlated logging; generic internal errors; role-authorized log stream (THR-018 controls) | Notification delivery swallow (GAP-025); audit tamper-evidence (GAP-012) |
| V8 Data protection | UTC discipline; activation-code hashing; demo-record distinctness (SR-D) | Retention policy TBD (GAP-030); encryption approach TBD (SR-S.008) |
| V9 Communications | TLS-only EST; CRL listener restriction; mTLS | Cipher policy untested (§5) |
| V10 Malicious code | CI gates; dependency advisories tracked | High advisories unremediated (GAP-005); no SBOM (GAP-031) |
| V11 Business logic | One-time activation; approval gating; issuer-mismatch guards; fail-closed semantics (SR-X) | Pending determinism (GAP-004); escape hatches (GAP-015) |
| V12 Files/resources | 64 KiB cap; PFX ACLs; sandbox isolation | Unencrypted key export in demo (GAP-017) |
| V13 API | Partial contract-first files; missing implemented paths/security schemes; Client empty and Schema stale (GAP-029) | OpenAPI.Client empty, Schema stale; no EST contract (GAP-029) |
| V14 Configuration | Documented settings; sandbox mode | Hardening checklist absent (GAP-037); dev bypass confinement (GAP-015) |

## 12. Open implementation questions

1. Which forwarder-authentication mechanism closes SR-S.005 (GAP-009), and is XFCC
   retained at all if browser/mTLS listener separation (GAP-024) proceeds?
2. Is `/csrattrs` (RFC 9908 semantics) required for the joint OR.NET test case, or is
   out-of-band profile documentation acceptable (GAP-001)?
3. Which production CRL lifetime/propagation policy (GAP-010) and which OCSP position?
4. Is server-side key generation in scope (GAP-002)?
5. What audit format target — internal hash-chained records first, ATNA mapping later
   (GAP-012)?
6. Does the production renewal model (GAP-026) rely on a device-resident agent, and
   does that change the EST profile?
7. Which secret store is the reference deployment (GAP-011)?

## 13. Disclaimer (restated)

**No third-party certification or standards-body approval is claimed.** This
document records implementation status, evidence provenance, and verification needs
only. EST, SDC, SDPi, ATNA, DICOM TLS, and control-framework conformity are
expressly not claimed (docs/ORNET-ALIGNMENT-2026-09-09.md review limits; README
disclaimer; KSPEC-000 §13). The repository remains a research prototype requiring
independent security assessment before any clinical or production use.

---

*End of KSPEC-011, version 0.2 (Draft). Approval state "Review Required"; no approval
is recorded or inferred.*
