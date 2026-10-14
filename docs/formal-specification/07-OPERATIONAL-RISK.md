# Kryptonian Formal Engineering Specification — Operational Risk and Reliability Assessment

| Field | Value |
|---|---|
| Document ID | KSPEC-007 |
| Title | Operational Risk and Reliability Assessment (FMEA) |
| Version | 0.2 (Draft) |
| Status | Draft — pending review |
| Created | 2026-10-13 |
| Baseline commit | `682f996c8447afec13bc1fce5b7f59e6a06efa25` (branch `feat/windows-sandbox-installer`, "Release Windows gateway and portable enrollment demo 0.1.4") |
| Revision history | 0.1 — 2026-10-13 — Initial draft FMEA covering 16 operational risk entries (ORK-001 … ORK-016) |
| Source references | KSPEC-000 (conventions); `docs/IMPLEMENTATION-TRACKER.md`; `docs/ARCHITECTURE.md`; `docs/DEPLOYMENT.md`; `docs/device-enrollment.md`; `docs/ca-backend-configuration.md`; `docs/email-notifications.md`; `docs/windows-installation.md`; `docs/windows-sandbox.md`; source under `src/RTSec.Kryptonian.{Api,Application,Infrastructure}`; `docker-compose.yml`; `deploy/`; `installer/windows/` |
| Author | _Name / Date / Signature_ |
| Reviewer | _Name / Date / Signature_ |
| Approver | _Name / Date / Signature_ |
| Reconciliation revision | 0.2 - 2026-10-13 - source/document audit and publication controls (KSPEC-016) |

---

## 1. Purpose and scope

This document is the operational risk and reliability assessment (KSPEC-007) for
RTSec.Kryptonian at baseline commit `682f996c8447afec13bc1fce5b7f59e6a06efa25`.
It applies Failure Mode and Effects Analysis (FMEA) style structure — severity,
occurrence, and detection ratings with explicit qualitative scales — to
operational failure modes that affect cybersecurity posture, network
connectivity, availability, and operational continuity of the gateway and the
device credential lifecycle it manages.

Scope:

- Gateway operational failure modes (enrollment, renewal, revocation, CRL
  publication, notifications, persistence, deployment, upgrades).
- Failure modes originating in external dependencies (CA backends, SMTP relay,
  network, clock sources, third-party dependencies).
- AS-IS mitigations present in the baseline, with evidence provenance per
  KSPEC-000 §5, and PROPOSED controls that require stakeholder approval.

Out of scope:

- Medical-device hazard classification, IEC 62304 lifecycle classification,
  ISO 14971 medical-device risk management, and clinical harm probabilities.
  Kryptonian is enterprise healthcare IT cybersecurity infrastructure — an EST
  (RFC 7030) certificate enrollment and credential lifecycle gateway. It is not
  a regulated medical device and performs no clinical functions (KSPEC-000 §3).
- Deliberate security attacks against the gateway; these are covered by the
  threat model (KSPEC-006, THR-*) except where operational behavior and threat
  behavior intersect (cross-referenced as noted).
- Clinical capability: device clinical function is never derived from
  Kryptonian state. Loss of a device credential degrades device *network
  identity and secure communication*, never clinical capability.

## 2. Assessment method, scales, and priority bands

### 2.1 Method

Each risk entry (identifier prefix `ORK-`, owned by this document per
KSPEC-000 §7) is analyzed as:

1. **Description and failure mode** — what goes wrong.
2. **Causes** — credible initiating events; no probabilities are assigned;
   occurrence ratings are qualitative anchor-based judgments, not measured
   failure rates.
3. **Effects** — operational consequences.
4. **AS-IS mitigations** — controls that exist at the baseline, each with
   repository evidence and a provenance class (VERIFIED / CODE-OBSERVED /
   DOCUMENTED / INFERRED / UNKNOWN per KSPEC-000 §5).
5. **Detection mechanisms** — how the failure is noticed, AS-IS and proposed.
6. **PROPOSED controls** — enhancements requiring stakeholder approval; never
   presented as normative without approval (KSPEC-000 §4).
7. **Recovery strategy** — AS-IS where implemented, PROPOSED where not.
8. **Linked requirements** — prefix-level references only (see §2.6).
9. **Verification methods** — T (Test), I (Inspection), A (Analysis),
   D (Demonstration) per KSPEC-000 §11.

### 2.2 Severity scale (S, 1–10, qualitative)

| Rating | Anchor |
|---|---|
| 1–2 | Negligible: no credential or availability impact; cosmetic log-only condition. |
| 3–4 | Minor: single-device credential impact, transient delay, or degraded admin visibility; recoverable by retry. |
| 5–6 | Moderate: multiple devices unable to enroll/renew, one operational function unavailable, or silent loss of an administrative control for a bounded period. |
| 7–8 | Major: gateway-wide enrollment/revocation outage, revocation-state integrity loss, or silent failure of a security-relevant control. |
| 9–10 | Critical: enterprise-wide loss of device credential trust, irreversible loss of CA/database state, or sustained silent security-control failure. |

### 2.3 Occurrence scale (O, 1–10, qualitative)

| Rating | Anchor (engineering judgment, not measured frequency) |
|---|---|
| 1–2 | Remote: requires multiple simultaneous preconditions or an event never yet observed. |
| 3–4 | Unlikely: possible under plausible but uncommon conditions. |
| 5–6 | Occasional: plausible in normal operation (e.g., known boundary behaviors recorded in `docs/IMPLEMENTATION-TRACKER.md`). |
| 7–8 | Likely: conditions occur routinely in the documented operating model. |
| 9–10 | Near-certain: will occur without operator intervention given documented behavior. |

### 2.4 Detection scale (D, 1–10, qualitative)

| Rating | Anchor |
|---|---|
| 1–3 | High detectability: an existing automated mechanism surfaces the failure (health endpoint, enrollment events, logs). |
| 4–6 | Moderate detectability: failure is logged but requires operator attention or an enabled, correctly subscribed notification channel. |
| 7–8 | Low detectability: failure is silently swallowed by design or only visible by manual inspection. |
| 9–10 | Very low detectability: no in-repo mechanism; detected only via external consequence. |

### 2.5 Risk priority numbers and priority bands

RPN = S × O × D (each 1–10, so RPN ∈ 1…1000).

| Band | RPN | Response |
|---|---|---|
| Critical | ≥ 216 | Requires PROPOSED control scheduling before any production deployment. |
| High | 108–215 | Requires PROPOSED control scheduling or a documented operational workaround. |
| Medium | 48–107 | Track; improve detection or mitigation opportunistically. |
| Low | < 48 | Accept with recorded rationale. |

Two overrides, regardless of RPN:

- Any risk with S ≥ 9 is at least **High**.
- Any risk with S ≥ 7 **and** no automated detection (D ≥ 7) is at least **High**.

### 2.6 Linked requirements — prefix-level references

KSPEC-003 contains the canonical Requirements Register. Area-level references below describe risk-control intent; KSPEC-009 §7.2 binds each ORK to concrete SR identifiers. Interoperability references use `SR-F-INT`; CA connector behavior uses `SR-F-CAI`. No area reference defines a new obligation.

## 3. Risk register (FMEA entries)

Each entry below states the failure mode, causes, effects, AS-IS mitigations
with provenance, detection, PROPOSED controls, recovery strategy, linked
requirement prefixes, and verification methods. Ratings are qualitative and
apply the scales of §2. Cross-references to KSPEC-006 threats are given where
an operational failure mode intersects threat behavior.

### ORK-001 — Certificate renewal failure

**Rating (AS-IS):** S=7, O=6, D=5 → RPN=210 → **High** (override: S≥7 with no
fully automated detection).

- **Description / failure mode:** A device's credential approaches or reaches
  `NotAfter` without a successful re-enrollment, so mTLS renewal itself and all
  certificate-authenticated communication become impossible. This is a dead-end
  state: re-enrollment requires a (still-valid) client certificate
  (`EstController.SimpleReenroll` returns 401 "Client certificate required for
  re-enrollment" otherwise), and activation codes bootstrap only the *first*
  certificate (`docs/device-enrollment.md`).
- **Causes:** (a) documented operating boundary — automatic renewal runs only
  while the sample device application (`Kryptonian.MedicalDevice`) is open
  (`docs/IMPLEMENTATION-TRACKER.md`, "Deliberate boundaries"); (b) CA backend
  failure or rejection during re-enrollment; (c) network interruption during
  the renewal window; (d) historical client-side key-generation defect
  (Schannel rejected ephemeral attached RSA keys — fixed with a runnable
  regression per the tracker, but demonstrates client-side fragility);
  (e) CSR/returned-certificate policy enforcement rejecting a previously
  acceptable request.
- **Effects:** Device loses network identity at certificate expiry; all
  certificate-authenticated traffic (including EST renewal and DICOM mTLS in
  the demonstration topology) fails closed; device must be re-registered and
  re-provisioned through the administrator activation flow.
- **AS-IS mitigations:**
  - `CertificateExpiryWatcher` scans daily (24 h interval, 2-min cold-start
    delay) for valid certificates inside the configured warning window whose
    device has not recorded a newer certificate, and fires a near-expiry
    notification; re-notification for the same certificate is suppressed for
    7 days (`CertificateExpiryWatcher.cs`, CODE-OBSERVED; summary documented in
    `docs/email-notifications.md`, DOCUMENTED).
  - Renewal is certificate-authenticated, generates a new key, and preserves
    the approved identity; one-time activation replay is rejected
    (tracker live-run evidence, VERIFIED).
  - Pending/async enrollment returns HTTP 202 with `Retry-After`
    (`EstController.HandleEnrollmentResult`, CODE-OBSERVED).
- **Detection:** Near-expiry email (requires enabled settings, subscribed
  recipients, and a reachable SMTP relay — see ORK-014); enrollment events and
  structured logs; no automated alerting on *repeated failed* re-enrollments
  beyond the rejection notification.
- **PROPOSED controls:** Out-of-application renewal operation as a deployment
  policy item (the tracker explicitly defers "Long-lived OS service operation…
  need deployment policy"); a defined renewal deadline policy (e.g., renew by
  N days before expiry with escalation); alerting on consecutive failed
  re-enrollments for the same device; renewal-attempt telemetry. [PROPOSED]
- **Recovery strategy:** AS-IS: once a certificate has expired, the device
  cannot renew and requires administrator re-registration (new pending record
  and activation code). PROPOSED: a documented emergency re-bootstrap path
  with operator dual control. [PROPOSED]
- **Linked requirements (prefix-level):** SR-C-* (certificate lifecycle),
  SR-R-* (reliability), SR-X-* (failure and recovery).
- **Verification methods:** T (renewal window and watcher behavior), D (live
  lifecycle demonstration per tracker), A (renewal-deadline policy review).

### ORK-002 — Device communication interruption

**Rating (AS-IS):** S=6, O=6, D=4 → RPN=144 → **High**.

- **Description / failure mode:** A device cannot reach the gateway EST
  endpoint (`/.well-known/est/...`) or the CRL endpoint, or the gateway cannot
  reach the active CA backend, for a period that disrupts enrollment, renewal,
  or revocation-status retrieval.
- **Causes:** network/switch/firewall outage; gateway process or host failure;
  EST listener not started; rate limiting (`AspNetCoreRateLimit`, 100
  requests/minute on `*:/.well-known/est/*` per `docs/ARCHITECTURE.md`) blocking
  a burst of legitimate clients; reverse-proxy misconfiguration of
  `X-Forwarded-For` (used by `EstController.GetClientIp`, CODE-OBSERVED).
- **Effects:** enrollment attempts fail (recorded as enrollment events);
  near-expiry devices may miss their renewal window (feeds ORK-001); CRL
  retrieval failures may cause peers to fail certificate validation depending
  on their policy.
- **AS-IS mitigations:**
  - Health endpoint `GET /api/status/health` reporting component status
    (database, ca_connectors) (`docs/DEPLOYMENT.md`, DOCUMENTED;
    `docker-compose.yml` healthcheck, CODE-OBSERVED).
  - Enrollment rejections recorded as persisted events and (optionally)
    emailed, including operation, subject DN, device ID, client IP, and reason
    (`docs/email-notifications.md`, DOCUMENTED).
  - 202-pending responses with `Retry-After` allow clients to back off
    rather than hard-fail (CODE-OBSERVED).
  - CRL documents are served from persisted issuer state with `nextUpdate`
    caching, so short gateway outages do not immediately invalidate cached
    CRLs at peers (`CertificateRevocationService.GetCrlAsync`, CODE-OBSERVED).
- **Detection:** health checks; enrollment-rejection notifications; structured
  logs (Serilog console + rolling files, DOCUMENTED). No external synthetic
  monitoring of the EST path is present in the repository (INFERRED from
  absence; `docs/DEPLOYMENT.md` marks Prometheus metrics as future).
- **PROPOSED controls:** external synthetic probe of `/cacerts` and CRL
  endpoints; rate-limit tuning review with capacity data; documented firewall/
  port requirements per deployment form (loopback-only by default in the
  Windows package — enabling external device connections is a separate network
  configuration milestone, `docs/windows-installation.md`, DOCUMENTED).
  [PROPOSED]
- **Recovery strategy:** AS-IS: devices retry; gateway restart re-enters
  service (restart persistence on populated SQLite demonstrated, tracker,
  VERIFIED). PROPOSED: runbook with failover/secondary gateway guidance.
  [PROPOSED]
- **Linked requirements (prefix-level):** SR-V-* (availability), SR-F-INT-*
  (interoperability), SR-R-*.
- **Verification methods:** T (health endpoint), D (retry/pending behavior),
  A (rate-limit and capacity analysis).

### ORK-003 — Certificate authority unavailability

**Rating (AS-IS):** S=7, O=5, D=3 → RPN=105 → **Medium** (close to High band).

- **Description / failure mode:** The active CA backend (Self-Signed, ACME,
  ADCS, or EJBCA connector) is unreachable or errors, so enrollments/renewals
  routed to it fail. Devices never choose a backend; the gateway routes to the
  single active backend (`docs/ca-backend-configuration.md`, DOCUMENTED).
- **Causes:** backend service outage; incorrect backend URL or credentials;
  harness/connector configuration drift; ADCS/EJBCA hospital interoperability
  remains **unverified** (`docs/IMPLEMENTATION-TRACKER.md`, DOCUMENTED) — real
  hospital CA behavior under failure is unknown (UNKNOWN for field behavior).
- **Effects:** all enrollments on the active profile fail until the backend is
  restored or another backend is activated; existing certificates and device
  records remain available (activation "affects future certificate enrollments
  only", DOCUMENTED).
- **AS-IS mitigations:**
  - Connector error handling converts HTTP failures and exceptions into
    `CertificateIssuanceResult.Failed(...)` with logged detail
    (`AdcsCaConnector.IssueCertificateAsync`, `EjbcaCaConnector.IssueCertificateAsync`,
    CODE-OBSERVED).
  - `TestConnectionAsync` per backend (UI **Test** button / `POST /api/cas/{id}/test`)
    returns a success flag and `false` (not an exception) when unreachable
    (CODE-OBSERVED; usage DOCUMENTED).
  - Health endpoint includes a `ca_connectors` component (DOCUMENTED).
  - Failure responses propagate to the device as EST errors; exceptions in
    `EstController` map to 4xx/5xx with logged cause (CODE-OBSERVED).
- **Detection:** backend **Test** action; health endpoint; enrollment failure
  events/logs. No periodic automated backend probing exists (INFERRED from
  absence).
- **PROPOSED controls:** scheduled automated backend health probe with alert;
  documented failover procedure to a secondary enabled backend (only one
  backend may be active at a time — DOCUMENTED); pre-activation validation
  gate requiring a successful Test before **Activate**. [PROPOSED]
- **Recovery strategy:** AS-IS: activate an alternate enabled backend
  (UI **Activate** / `POST /api/cas/{backendId}/activate`); retry failed
  enrollments. Revocation against a disabled backend returns 503
  "Certificate authority is unavailable"
  (`CertificateRevocationService`, CODE-OBSERVED). PROPOSED: documented
  backend-outage runbook. [PROPOSED]
- **Linked requirements (prefix-level):** SR-C-*, SR-F-INT-*, SR-V-*, SR-X-*.
- **Verification methods:** T (connector failure-path tests), D (backend test/
  activate flow), A (interoperability gap analysis — also feeds KSPEC-010).

### ORK-004 — Incorrect certificate policy configuration

**Rating (AS-IS):** S=7, O=5, D=6 → RPN=210 → **High** (override not triggered:
D<7; band reached on RPN).

- **Description / failure mode:** An EST profile or CA backend is configured
  with the wrong policy bundle — subject-DN rules, key usage, validity,
  trust policy, or client-certificate requirements — so the gateway issues
  credentials that violate institutional policy, or rejects legitimate devices.
- **Causes:** manual profile editing via UI/REST; partial-update semantics
  misapplied; trust-thumbprint lists mis-entered; default-off chain validation
  (`validate_client_certificate_chain` defaults to `false` in schema upgrades,
  `SchemaUpgrades.cs`, CODE-OBSERVED); connector JSON config precedence rules
  (config field → primary URL → environment fallback → default) misunderstood
  (`docs/ca-backend-configuration.md`, DOCUMENTED).
- **Effects:** over-permissive policy → credentials inconsistent with policy
  (cross-ref KSPEC-006 threats THR-* for abuse); under-permissive policy →
  enrollment failures and device onboarding outage.
- **AS-IS mitigations:**
  - CSR and returned-certificate policy enforcement at issuance (PR #43,
    tracker; behavior VERIFIED in executed tests).
  - Partial updates preserve omitted trust lists (PR #47, tracker).
  - Unknown EST profile labels are rejected rather than falling back
    (PR #37, tracker).
  - Backend configuration **Test** action validates connectivity before use
    (DOCUMENTED).
  - Profile trust policy persisted durably with additive upgrades (PR #41,
    tracker).
- **Detection:** enrollment events record rejections with reason; policy
  violations at issuance are logged. No diffing/audit of profile changes is
  present in the baseline (INFERRED from absence).
- **PROPOSED controls:** profile change audit trail with before/after values;
  pre-save policy linting (e.g., warning when chain validation is disabled);
  staged profile activation (test profile → review → promote); documented
  naming/ownership of profiles. [PROPOSED]
- **Recovery strategy:** AS-IS: correct the profile via admin UI/REST;
  re-issue affected credentials as required by policy. Note that issued
  certificates are not recalled automatically — any recovery requiring
  invalidation of already-issued certificates depends on the revocation
  capability of the backend (ORK-008).
- **Linked requirements (prefix-level):** SR-Z-* (authorization), SR-S-*,
  SR-C-*, SR-O-*.
- **Verification methods:** I (configuration review procedure), T (policy
  enforcement tests), D (activation gate demonstration).

### ORK-005 — Unauthorized trust relationship

**Rating (AS-IS):** S=9, O=3, D=5 → RPN=135 → **High** (override: S≥9 ⇒ at
least High). Intersects KSPEC-006 threats (THR-*); retained here for the
operational (misconfiguration/misprovisioning) dimension.

- **Description / failure mode:** The gateway, the DIMSE-TLS proxy, or a client
  comes to trust an issuer or peer that institutional policy did not authorize,
  allowing a rogue device or intermediary to be accepted.
- **Causes:** mis-pinned `trusted_client_ca_thumbprints` on a profile; CA
  private-key/PFX exposure; silent installation of trust into OS stores
  (explicitly avoided — the Windows package adds only a separate administration
  CA to the current user's store after an explicit prompt and never installs a
  device-issuing CA machine-wide, `docs/windows-installation.md`, DOCUMENTED);
  accidental activation of the wrong backend as the active CA.
- **Effects:** issuance of gateway-recognized credentials to unauthorized
  clients, or acceptance of unauthorized peers in mTLS; potentially
  enterprise-wide credential trust loss (S=9 anchor).
- **AS-IS mitigations:**
  - Trusted-forwarding guard rejects direct forwarded-certificate spoofing;
    the HTTP listener denies non-CRL requests (PR #39 + tracker live evidence,
    VERIFIED).
  - Authenticated enrollment identity binding (PR #38, VERIFIED).
  - DIMSE proxy operational trust checks: issuer, hostname, purpose, online
    revocation; expired replacement PFX fails closed (tracker loopback proxy
    tests, VERIFIED).
  - Administration CA trust is per-user and explicit; no silent machine-wide
    trust (DOCUMENTED).
  - Trust policy persistence is additive and durable (PR #41, VERIFIED via
    restart test on populated SQLite).
- **Detection:** enrollment-rejection notifications (reason recorded); logs of
  client-certificate validation failures (`EstController` warns with reason,
  CODE-OBSERVED). No automated reconciliation of configured trust lists against
  an approved baseline (INFERRED from absence).
- **PROPOSED controls:** periodic trust-baseline reconciliation report;
  dual-control change for trust-list edits; CA key/PFX custody and access
  review; automated alert when a new issuer fingerprint appears in trust
  state. [PROPOSED]
- **Recovery strategy:** AS-IS: remove the unauthorized profile trust entries
  via admin API; revoke affected certificates where the backend supports it
  (ORK-008 limits apply); the user-facing trust-removal action is
  `POST /api/devices/{id}/remove`, after which renewals are rejected
  (`docs/sequence-diagrams/README.md`, DOCUMENTED).
- **Linked requirements (prefix-level):** SR-A-* (authentication), SR-S-*,
  SR-Z-*, SR-C-*.
- **Verification methods:** D (spoof-rejection and trust-lifecycle
  demonstrations), T (trust-policy regression tests), I (trust audit).

### ORK-006 — Database corruption

**Rating (AS-IS):** S=9, O=4, D=6 → RPN=216 → **Critical**.

- **Description / failure mode:** Loss of integrity or availability of the
  persistence store (SQLite file in the Windows package; PostgreSQL 16 volume
  in the container deployment), destroying device records, certificate records,
  issuer CRL state, EST profiles, notification settings, and audit trails.
- **Causes:** disk/media failure; SQLite file corruption (e.g., power loss on
  host); PostgreSQL data-volume loss (`postgres_data` named volume in
  `docker-compose.yml`, CODE-OBSERVED, with **no automated backup in the
  compose file** — INFERRED from absence); failed or partial schema upgrade
  (ORK-011); manual deletion (uninstall intentionally retains private state,
  but external cleanup processes are unrestrained).
- **Effects:** enterprise-wide loss of credential lifecycle state: enrollment
  and renewal impossible; revocation state and CRL publication lost
  (`issuer_crl_states`); notification/audit history lost. Matches the S=9–10
  anchor ("irreversible loss of CA/database state").
- **AS-IS mitigations:**
  - Transactional revocation and CRL persistence with explicit
    begin/rollback/commit and concurrency-safe persistence attempts
    (`UnitOfWork.TryPersistRevocationStateAsync`,
    `CertificateRevocationService`, CODE-OBSERVED).
  - Gateway restart on populated SQLite preserved configured profile, issuer,
    and devices (tracker live evidence, VERIFIED) — demonstrates durability
    across clean restarts, not corruption recovery.
  - Windows installer: "Back up database, CA and encryption keys together"
    is operator guidance only; "Do not treat MSI rollback as a database
    rollback" (`docs/windows-installation.md`, DOCUMENTED).
  - Azure plans note 7-day point-in-time backups for the Postgres instance in
    that specific deployment (`plans/azure-deployment.md`, DOCUMENTED; not a
    general guarantee).
- **Detection:** health endpoint `database` component (DOCUMENTED); startup
  failures. Silent logical corruption is unlikely to be detected automatically
  (INFERRED).
- **PROPOSED controls:** scheduled, verified, off-host backups (full design in
  §6); pre-upgrade snapshots; SQLite integrity checks (e.g., `PRAGMA
  integrity_check`) with alerting; documented restore drills. [PROPOSED]
- **Recovery strategy:** AS-IS: none beyond reinstallation and re-provisioning
  from scratch; MSI rollback explicitly is not a database rollback. PROPOSED:
  restore procedure of §6 with defined RPO/RTO targets TBD by stakeholders.
  [PROPOSED]
- **Linked requirements (prefix-level):** SR-D-* (data management), SR-X-*,
  SR-O-*.
- **Verification methods:** T (backup/restore drill), A (corruption-scenario
  analysis), I (backup schedule inspection).

### ORK-007 — Certificate expiration (unmanaged)

**Rating (AS-IS):** S=7, O=5, D=4 → RPN=140 → **High**.

- **Description / failure mode:** A valid device certificate passes into
  expired status without renewal or operator action; device loses the ability
  to communicate or renew (same dead-end as ORK-001 but viewed from the
  gateway-side monitoring and escalation angle).
- **Causes:** missed notifications (ORK-014); device offline during the entire
  warning window; warning window (`ExpiryWarningDays`) set too short;
  7-day re-notification suppression delaying repeated alerts
  (`CertificateExpiryWatcher`, CODE-OBSERVED).
- **Effects:** device connectivity failure; security teams may be unaware a
  revoked/expired credential is still presented by peers unless those peers
  enforce expiry (peers do fail closed on expired replacement PFX in proxy
  tests, VERIFIED).
- **AS-IS mitigations:**
  - Daily watcher scan with per-certificate warning window and suppression
    state (`LastExpiryNotifiedAt`), skipping already-expired certificates and
    certificates whose device has a newer one (CODE-OBSERVED).
  - Expiry emails include subject DN, device ID, serial, expiration time, and
    days remaining (DOCUMENTED).
  - Peers enforce expiry and online revocation, so expired credentials fail
    closed at the peer (VERIFIED).
- **Detection:** watcher-driven email; dashboard certificate listing. Already-
  expired certificates are deliberately skipped by the watcher ("already
  expired — different concern", CODE-OBSERVED) — meaning the gateway itself
  does not raise a *post-expiry* alert, an AS-IS detection gap.
- **PROPOSED controls:** post-expiry state alerting/escalation; expired-
  certificate lifecycle policy (retire/remove records); adjustable escalation
  when warnings are unacknowledged. [PROPOSED]
- **Recovery strategy:** AS-IS: device re-registration with new activation
  code (ORK-001 recovery). PROPOSED: bulk expiry remediation runbook.
  [PROPOSED]
- **Linked requirements (prefix-level):** SR-C-*, SR-O-*, SR-R-*.
- **Verification methods:** T (watcher scan tests), D (expiry demonstration),
  I (notification policy review).

### ORK-008 — Failed revocation

**Rating (AS-IS):** S=8, O=6, D=5 → RPN=240 → **Critical**.

- **Description / failure mode:** Revocation of a compromised or retired
  credential fails or is unavailable, so a revoked certificate remains usable
  at peers until CRL propagation/validity windows elapse — or, for some
  backends, revocation cannot be performed at all through the gateway.
- **Causes:** remote CA revocation not implemented — the ADCS SCEP connector
  logs "does not implement revocation" and returns `false`
  (`AdcsCaConnector.RevokeCertificateAsync`, CODE-OBSERVED; `docs/adcs-real-backend-demo.md`
  notes revocation would need a separate channel such as certutil / CA Web
  Services, DOCUMENTED); EJBCA revoke requires `IssuerDn` configuration and
  returns `false` on failure or misconfiguration (CODE-OBSERVED); unsupported
  CRL generation fails closed with 501 (CODE-OBSERVED); issuer/CA mismatch or
  rotated-CA guards reject the operation with 409 (CODE-OBSERVED);
  ADCS/EJBCA hospital interoperability unverified (DOCUMENTED).
- **Effects:** a compromised credential may continue to authenticate until
  peers observe revocation; CRL lifetime is a 60-second prototype cadence
  (`PrototypeCrlLifetime = 60 s`, CODE-OBSERVED) — a deliberate demo value
  that must not be assumed production-appropriate without review.
- **AS-IS mitigations (fail-closed orientation):**
  - Remote-CA revocation **fails closed**: unsupported connectors or CRL
    generation produce error responses (501) and the certificate state is not
    changed (`CertificateRevocationService`, tracker: "Unsupported remote CA
    revocation fails closed", DOCUMENTED + CODE-OBSERVED).
  - Revocation is transactional: certificate status, `revoked_at`, and issuer
    CRL state persist atomically; concurrency conflicts return 409 with a
    retry hint (CODE-OBSERVED; demonstration VERIFIED — administrator
    revocation followed by native online X509Chain reporting Revoked without
    flushing the system CRL cache).
  - Issuer fingerprint and rotated-CA checks prevent revoking against the
    wrong issuer (CODE-OBSERVED).
  - Peer-side online revocation checking is enabled for device/client and
    DICOM peers (VERIFIED); proxy connections have bounded lifetimes rather
    than instant mid-association revocation (DOCUMENTED boundary).
- **Detection:** revocation API error responses (404/409/501/503/500 with
  reason) and logs; no proactive monitoring of revocation success rates
  (INFERRED from absence).
- **PROPOSED controls:** real revocation integrations for ADCS (certutil / CA
  Web Services) and verified EJBCA operation; revocation-outcome reporting in
  the admin UI; monitoring of CRL `nextUpdate` staleness; decision review of
  the 60-second CRL lifetime for production. [PROPOSED]
- **Recovery strategy:** AS-IS: retry revocation after fixing the cause
  (e.g., configure `IssuerDn`, re-enable backend); where the connector cannot
  revoke, revoke out-of-band at the CA and rely on peer-side checks.
  PROPOSED: compensating revocation runbook per backend type. [PROPOSED]
- **Linked requirements (prefix-level):** SR-C-* (certificate lifecycle),
  SR-S-*, SR-F-INT-*.
- **Verification methods:** D (revocation demonstration), T (fail-closed
  connector tests), A (CRL lifetime and propagation analysis).

### ORK-009 — Clock synchronization errors

**Rating (AS-IS):** S=8, O=4, D=7 → RPN=224 → **Critical** (override: S≥7 with
D≥7).

- **Description / failure mode:** Incorrect system time or timezone handling
  corrupts certificate-lifecycle decisions — expiry windows, CRL
  `thisUpdate`/`nextUpdate`, activation-code expiry, revocation visibility.
- **Causes:** NTP drift or outage on the gateway host; mixed UTC/local
  DateTime kinds. **Recorded precedent:** SQLite's unspecified `DateTimeKind`
  shifted refreshed CRL revocation times five hours into the future on the
  development host; fixed with a runnable regression (tracker,
  "Live checks exposed two bugs that mocked tests missed", VERIFIED as fixed;
  demonstrates the failure class).
- **Effects:** CRLs that appear valid/invalid at the wrong times; peers may
  reject or accept revocation state incorrectly (failing closed or
  mis-trusting); activation codes and warning windows compute wrongly.
- **AS-IS mitigations:**
  - Fixed regression for the SQLite `DateTime` kind bug (tracker, VERIFIED).
  - Consistent `DateTime.UtcNow` usage in lifecycle computations
    (`CertificateRevocationService`, `CertificateExpiryWatcher`,
    CODE-OBSERVED — inspection, not an exhaustive audit).
  - `nextUpdate` bounds CRL validity at the issuer's `NotAfter`
    (`GetNextUpdate`, CODE-OBSERVED).
- **Detection:** the original bug was caught only by live end-to-end checks,
  not by mocked tests (tracker, VERIFIED). No runtime clock-drift monitoring
  exists (INFERRED from absence).
- **PROPOSED controls:** host NTP requirements in deployment documentation;
  invariant/regression tests for UTC round-tripping across both persistence
  providers; startup warning when local time zone ≠ UTC or when skew exceeds a
  configurable threshold. [PROPOSED]
- **Recovery strategy:** AS-IS: re-synchronize host clock; CRL/`nextUpdate`
  state self-corrects on the next refresh cycle because `thisUpdate`/`nextUpdate`
  are recomputed per generation (CODE-OBSERVED). PROPOSED: documented
  time-incident runbook. [PROPOSED]
- **Linked requirements (prefix-level):** SR-R-*, SR-C-*, SR-D-*.
- **Verification methods:** T (UTC-kind regression tests), A (time-handling
  audit), D (skewed-clock demonstration).

### ORK-010 — Configuration errors

**Rating (AS-IS):** S=7, O=6, D=5 → RPN=210 → **High**.

- **Description / failure mode:** Gateway-level configuration error — weak or
  default credentials, wrong ports/listeners, mis-set environment variables,
  insecure SMTP options — degrades security or availability.
- **Causes:** compose defaults shipped for development (`POSTGRES_PASSWORD`
  default `kryptonian_dev_password`, `ADMIN_API_KEYS` default
  `dev-api-key-change-in-production`, seeded initial admin
  `Kryptonian__Auth__InitialAdminPassword: rtsec2026`, `ASPNETCORE_ENVIRONMENT`
  defaulting to Development — `docker-compose.yml`, CODE-OBSERVED); SMTP
  "Trust SMTP server certificate" toggle that skips TLS certificate chain
  validation (`MailKitSmtpSender` sets a permissive
  `ServerCertificateValidationCallback`, CODE-OBSERVED; documented as
  leave-off-by-default, DOCUMENTED); environment fallbacks for connector
  settings (DOCUMENTED).
- **Effects:** weak administrative authentication; MITM exposure of
  notification transport; enrollment outages from wrong endpoints; JWT secret
  shorter than the documented ≥32-character production requirement
  (`docker-compose.yml` comment, DOCUMENTED).
- **AS-IS mitigations:**
  - Production security checklist in `docs/DEPLOYMENT.md` requiring strong
    generated credentials (DOCUMENTED guidance, not enforced in code).
  - First-run administrator wizard with no shared default password in the
    Windows package (`docs/windows-installation.md`, DOCUMENTED).
  - Notification passwords stored encrypted via ASP.NET Data Protection and
    never returned to the UI (DOCUMENTED; `DataProtectionService`,
    CODE-OBSERVED).
  - ACME connector config validation (`config.Validate()` rejects missing
    directory URL/email, unsupported challenge types, half-provided EAB,
    CODE-OBSERVED).
- **Detection:** manual checklist review; startup errors from invalid config.
  No automated "production sanity" gate (e.g., refusing default credentials in
  Production environment) is present (INFERRED from absence).
- **PROPOSED controls:** startup validation that fails (or loudly warns) when
  Development defaults are detected in a non-Development environment;
  configuration export/diff tooling for change review; documented baseline
  configuration with checksums. [PROPOSED]
- **Recovery strategy:** AS-IS: correct settings via environment/UI and
  restart; rotate any exposed credential. PROPOSED: credential-rotation
  runbook (ORK-015). [PROPOSED]
- **Linked requirements (prefix-level):** SR-B-* (deployment), SR-S-*,
  SR-O-*.
- **Verification methods:** I (configuration checklist audit), T (startup
  validation tests once proposed controls exist), D (guided setup
  demonstration).

### ORK-011 — Software upgrade failure

**Rating (AS-IS):** S=8, O=5, D=6 → RPN=240 → **Critical**.

- **Description / failure mode:** A schema upgrade, container image update, or
  Windows installer upgrade leaves the gateway unable to start, loses data, or
  breaks an existing installation.
- **Causes:** PostgreSQL schema upgrades are implemented but **only SQLite
  persistence was exercised** (`docs/IMPLEMENTATION-TRACKER.md`, DOCUMENTED);
  `SchemaUpgrades.EnsureTrustPolicyAsync` runs raw additive DDL
  (`ALTER TABLE ... ADD COLUMN IF NOT EXISTS`, `CREATE TABLE IF NOT EXISTS`,
  index replacement) outside an explicit transaction/backup boundary
  (CODE-OBSERVED); container image updates replace the running API without an
  in-repo rollback artifact; MSI upgrade keeps existing CA and database but
  install/repair/upgrade/uninstall acceptance on a clean target remains
  **unverified** (`docs/windows-installation.md`, DOCUMENTED); Windows Sandbox
  guest state disappears on close (DOCUMENTED).
- **Effects:** startup failure; missing columns/tables causing runtime errors;
  in the worst case data loss (S=8 anchor: gateway-wide outage; corruption
  path overlaps ORK-006).
- **AS-IS mitigations:**
  - Schema upgrades are explicitly **additive**: "Existing installations use
    EnsureCreated, not EF migrations. Keep this additive: never recreate a
    populated database" (`SchemaUpgrades.cs` header comment, CODE-OBSERVED).
  - Additive upgrade design reviewed in PR #41 (tracker).
  - MSI upgrade preserves existing CA and database; readiness health check
    failure fails installation instead of reporting success
    (`docs/windows-installation.md`, DOCUMENTED).
  - Developer lifecycle evidence includes persistence across restart
    (VERIFIED).
- **Detection:** startup logs; health endpoint; MSI readiness check (fails
  installation on unhealthy readiness — DOCUMENTED). Partial-failure states
  between upgrade steps are not automatically detected (INFERRED).
- **PROPOSED controls:** mandatory pre-upgrade backup of database + CA + Data
  Protection key ring (§6); upgrade rehearsal on a snapshot in staging for
  **both** persistence providers, with explicit PostgreSQL exercise;
  documented image tag/rollback procedure for container deployments;
  upgrade-run checklist and go/no-go criteria. [PROPOSED]
- **Recovery strategy:** AS-IS: reinstall/repair MSI (retains private state);
  manual SQL remediation of additive schema. PROPOSED: restore-from-backup
  procedure (§6); documented schema-rollback position (additive upgrades make
  rollback by deletion unnecessary, but this shall be verified per release).
  [PROPOSED]
- **Linked requirements (prefix-level):** SR-B-*, SR-D-*, SR-X-*, SR-O-*.
- **Verification methods:** T (upgrade rehearsal tests), D (installer
  upgrade/uninstall demonstrations on clean targets), I (release checklist).

### ORK-012 — Backup and recovery failure

**Rating (AS-IS):** S=9, O=5, D=8 → RPN=360 → **Critical** (override: S≥9 and
S≥7 with D≥7).

- **Description / failure mode:** No mechanism exists in the baseline to back
  up or restore gateway state; when state loss occurs (ORK-006, ORK-011),
  recovery fails or is impossible, or a restore attempt itself fails because
  prerequisite material (CA key, Data Protection key ring, passwords) was never
  captured together.
- **Causes:** absence of implemented backup tooling (repository search for
  backup mechanisms returns only documentation admonitions and unrelated
  variable names — INFERRED from absence, CODE-OBSERVED for compose and
  deploy scripts containing none); operator guidance ("Back up database, CA
  and encryption keys together") exists only as documentation
  (`docs/windows-installation.md`, DOCUMENTED); Data Protection key ring not
  backed up with the database.
- **Effects:** after a database/volume loss, device records, certificate
  records, CRL state, profiles, and users cannot be restored; encrypted SMTP
  credentials become undecryptable if the Data Protection key ring is lost
  even when the database is restored (INFERRED from Data Protection semantics;
  `DataProtectionService` CODE-OBSERVED).
- **AS-IS mitigations:**
  - Documentation admonition to back up database, CA and encryption keys
    together before upgrades (DOCUMENTED).
  - MSI uninstall intentionally retains `C:\ProgramData\KryptonianSandbox`
    private state, reducing accidental loss during uninstall
    (DOCUMENTED).
  - Azure-plan deployment relies on platform point-in-time restore for that
    specific hosted instance (DOCUMENTED; deployment-specific, not a product
    capability).
- **Detection:** none — absence of backups is typically discovered only during
  an incident (D=8).
- **PROPOSED controls:** full backup/restore design in §6 (scope, schedule,
  retention, verification, drill requirements, RPO/RTO targets TBD by
  stakeholders). [PROPOSED]
- **Recovery strategy:** AS-IS: none for gateway state beyond manual
  reconstruction and full device re-enrollment. PROPOSED: §6 procedure with
  quarterly restore drills. [PROPOSED]
- **Linked requirements (prefix-level):** SR-X-* (failure and recovery),
  SR-D-*, SR-O-*.
- **Verification methods:** T (restore drill), I (backup configuration
  inspection), A (recovery-time analysis).

### ORK-013 — Network partitioning

**Rating (AS-IS):** S=6, O=4, D=5 → RPN=120 → **High**.

- **Description / failure mode:** A partition between devices and gateway, or
  between gateway and CA/SMTP relay, isolates parts of the estate while the
  gateway itself reports healthy.
- **Causes:** VLAN/segmentation changes; firewall rule updates; hospital
  network maintenance; default loopback-only listeners in the Windows package
  (external device connectivity requires a separate, not-yet-performed network
  configuration milestone — DOCUMENTED); proxy connection lifetimes bounded
  rather than unlimited, so long-lived associations end during partitions
  (DOCUMENTED boundary).
- **Effects:** devices in isolated segments cannot enroll/renew (feeding
  ORK-001); CRL retrieval failures; DICOM mTLS associations terminated at
  their bounded lifetime; notifications cannot leave the segment (ORK-014).
- **AS-IS mitigations:**
  - Health endpoint and container healthchecks detect *local* health only.
  - Persistent CRL state with `nextUpdate` gives peers a bounded tolerance to
    gateway unavailability (CODE-OBSERVED).
  - Explicitly documented network expectations (ports 7443/7444/7445 in the
    Windows package; 5000/5001 in compose) (DOCUMENTED).
- **Detection:** enrollment-rejection events cease from isolated segments —
  the *absence* of events is not itself alerted on (INFERRED gap); no network
  synthetic monitoring exists.
- **PROPOSED controls:** per-segment synthetic enrollment/CRL probes;
  monitoring of "devices heard from" staleness (alert when a previously active
  device reports nothing for N days); documented segmentation/port matrix per
  deployment form. [PROPOSED]
- **Recovery strategy:** AS-IS: network remediation; devices retry enrollment
  when connectivity returns. PROPOSED: alternate enrollment path (documented
  maintenance window procedure). [PROPOSED]
- **Linked requirements (prefix-level):** SR-V-*, SR-F-INT-*, SR-R-*.
- **Verification methods:** D (partition drill), T (retry/pending behavior),
  A (segmentation design review).

### ORK-014 — Notification delivery failure

**Rating (AS-IS):** S=6, O=6, D=7 → RPN=252 → **Critical** (override: S≥7 not
met at S=6; band reached on RPN).

- **Description / failure mode:** Near-expiry or enrollment-rejection email
  notifications are silently not delivered — because SMTP is unreachable, the
  master/event/subscription switches are off, or the recipient list is wrong —
  removing the operator's primary automated signal for ORK-001/ORK-007.
- **Causes:** SMTP relay outage or wrong host/port/TLS/auth settings; Data
  Protection key-ring loss making the stored SMTP password undecryptable
  (INFERRED from `NotificationDispatcher.BuildDispatchConfig` unprotect call,
  CODE-OBSERVED); three-layer enablement model (master switch → global event
  switch → per-recipient subscription) where any layer silently disables an
  event; the documented caveat that "the test email does not prove that event
  subscriptions are enabled" (`docs/email-notifications.md`, DOCUMENTED).
- **Effects:** silent loss of expiry and rejection alerts; operators learn of
  expiring credentials only after connectivity breaks. Note the AS-IS design
  intent: "production notification failures are logged and swallowed so email
  outages do not block enrollment requests" (DOCUMENTED;
  `NotificationDispatcher` catch-all, CODE-OBSERVED) — availability is
  protected at the cost of alert durability.
- **AS-IS mitigations:**
  - Failures logged with context ("Failed to dispatch … notification")
    (CODE-OBSERVED).
  - **Send test email** action validating SMTP connectivity before saving
    (DOCUMENTED; controller returns the failure reason, CODE-OBSERVED).
  - Recipient/settings REST endpoints for automation
    (`/api/settings/notifications[...]`, DOCUMENTED).
  - SMTP passwords stored encrypted, never returned to the UI (DOCUMENTED).
  - MailKit upgraded to patched 4.16.0 with BouncyCastle 2.6.2 (tracker,
    VERIFIED build change).
- **Detection:** log-only (D=7): delivery failures leave no persistent event
  record and no retry queue (CODE-OBSERVED — `SendAsync` exceptions are caught
  and logged).
- **PROPOSED controls:** persistent notification-outcome record with retry
  queue and backoff; a notification watchdog independent of the gateway's own
  SMTP path (e.g., periodic heartbeat mail whose absence is alerted by an
  external monitor); configuration self-test that exercises the full
  enablement chain, not just SMTP connectivity. [PROPOSED]
- **Recovery strategy:** AS-IS: fix SMTP settings via UI (password-update
  semantics: `null` keeps, empty clears, non-empty replaces — DOCUMENTED);
  missed alerts must be reconstructed from enrollment events and certificate
  records. PROPOSED: replay of unsent notifications after outage. [PROPOSED]
- **Linked requirements (prefix-level):** SR-O-*, SR-R-*, SR-U-* (audit).
- **Verification methods:** T (delivery-failure tests), D (watchdog
  demonstration), I (subscription configuration audit).

### ORK-015 — Secrets / credential rotation failure

**Rating (AS-IS):** S=8, O=4, D=7 → RPN=224 → **Critical** (override: S≥7 with
D≥7).

- **Description / failure mode:** Rotation of a secret or credential — API
  keys, JWT secret, CA PFX/password, SMTP credentials, Data Protection key
  ring, DIMSE proxy PFX — fails, is missed, or breaks a dependent component.
- **Causes:** no automated rotation exists in the baseline (INFERRED from
  absence); DIMSE proxy PFX renewal is **external** and reload is demonstrated,
  not automated CA renewal (tracker boundary, DOCUMENTED); rotating the CA
  certificate blocks revocation of previously issued certificates — the
  revocation service returns 409 "Configured CA certificate has rotated; create
  a new CA backend before revoking certificates" (CODE-OBSERVED); JWT secret
  rotation invalidates issued tokens; Data Protection key-ring rotation
  invalidates stored SMTP passwords (INFERRED).
- **Effects:** enrollment outages; peers unable to trust renewed intermediaries
  until PFX is replaced; revoked-certificate management gaps for certificates
  issued by the retired CA key; administrative lockouts.
- **AS-IS mitigations:**
  - Atomic one-time activation (PR #42) prevents replay during bootstrap
    (VERIFIED).
  - Externally rotated PFX reload demonstrated with fresh handshakes observing
    atomic replacement; expired replacement fails closed (tracker, VERIFIED).
  - API-key revocation endpoints exist for user credentials
    (`UsersController`/UI revoke actions, CODE-OBSERVED).
  - Rotated-CA guard above prevents wrong-issuer revocation rather than
    corrupting state (CODE-OBSERVED).
- **Detection:** component failure at next use (auth failures in logs); no
  expiry/cadence tracking for secrets (INFERRED from absence).
- **PROPOSED controls:** secret inventory with rotation cadences and owners;
  documented rotation runbooks per secret class (JWT, API keys, CA PFX, SMTP,
  Data Protection key ring); pre-rotation backup (§6); staged CA-key rotation
  design (dual CA backends during transition) requiring stakeholder approval.
  [PROPOSED]
- **Recovery strategy:** AS-IS: reconfigure and restart; device
  re-enrollment where trust is broken. PROPOSED: documented recovery from
  key-ring loss (restore keys first, per §6). [PROPOSED]
- **Linked requirements (prefix-level):** SR-S-*, SR-A-*, SR-O-*.
- **Verification methods:** D (rotation drill), T (post-rotation regression
  suite), I (secret inventory audit).

### ORK-016 — Dependency vulnerability exposure

**Rating (AS-IS):** S=7, O=5, D=4 → RPN=140 → **High**.

- **Description / failure mode:** A known vulnerability in a runtime, native,
  or UI dependency remains unpatched in a deployment, exposing the gateway or
  its host.
- **Causes:** **recorded, open transitive advisories** in the baseline:
  high advisories in `SQLitePCLRaw.lib.e_sqlite3 2.1.6` and
  `System.Security.Cryptography.Xml 8.0.2`, explicitly recorded as
  "deployment blockers requiring a separately tested framework/native-provider
  update, not evidence of a clean security audit"
  (`docs/IMPLEMENTATION-TRACKER.md`, DOCUMENTED); native SQLite provider
  updates carry persistence-compatibility risk; UI/npm supply chain.
- **Effects:** exploitable gateway or host components; potential privilege
  escalation or data exposure; failed deployment-security gates.
- **AS-IS mitigations:**
  - Compatible dependency security updates with audit disclosure (PR #52);
    MailKit upgraded to patched 4.16.0 with required BouncyCastle 2.6.2
    (tracker, VERIFIED build change).
  - UI lockfile refresh reduced npm audit from six vulnerable packages to
    zero; CI now **rejects high/critical UI dependency advisories**
    (tracker, VERIFIED CI behavior).
  - GitHub Actions CI builds UI and tests the solution (tracker, VERIFIED).
  - Honest disclosure that these measures are "not a whole-system audit"
    (DOCUMENTED).
- **Detection:** CI dependency audit for the UI; manual `dotnet list
  package --vulnerable` runs are not automated for the full solution
  (INFERRED from absence).
- **PROPOSED controls:** automated full-solution vulnerability scanning in CI
  (NuGet + npm + container base images) with advisory-based gates; SBOM
  publication per release; tracked remediation plan for the two recorded
  deployment blockers; scheduled dependency-update cadence. [PROPOSED]
- **Recovery strategy:** AS-IS: apply compatible update and redeploy.
  PROPOSED: emergency-patch runbook with pre-approval for out-of-band
  dependency updates. [PROPOSED]
- **Linked requirements (prefix-level):** SR-S-*, SR-B-*, SR-O-*.
- **Verification methods:** T (CI audit gates), I (SBOM and advisory review),
  A (exposure analysis).

## 4. FMEA summary table

Ratings use the §2 scales; bands per §2.5. All ratings are qualitative
engineering judgments recorded for prioritization, not measured probabilities.

| ORK | Failure mode | S | O | D | RPN | Band | Primary PROPOSED direction |
|---|---|---|---|---|---|---|---|
| ORK-001 | Certificate renewal failure | 7 | 6 | 5 | 210 | High | Out-of-app renewal operation; renewal-deadline policy |
| ORK-002 | Device communication interruption | 6 | 6 | 4 | 144 | High | External synthetic probes; rate-limit review |
| ORK-003 | CA unavailability | 7 | 5 | 3 | 105 | Medium | Automated backend probing; failover runbook |
| ORK-004 | Incorrect certificate policy configuration | 7 | 5 | 6 | 210 | High | Profile change audit; pre-save policy linting |
| ORK-005 | Unauthorized trust relationship | 9 | 3 | 5 | 135 | High¹ | Trust-baseline reconciliation; dual control |
| ORK-006 | Database corruption | 9 | 4 | 6 | 216 | Critical | Verified off-host backups; restore drills |
| ORK-007 | Certificate expiration (unmanaged) | 7 | 5 | 4 | 140 | High | Post-expiry alerting; escalation policy |
| ORK-008 | Failed revocation | 8 | 6 | 5 | 240 | Critical | Real ADCS/EJBCA revocation; CRL-staleness monitoring |
| ORK-009 | Clock synchronization errors | 8 | 4 | 7 | 224 | Critical¹ | NTP requirements; UTC invariant tests |
| ORK-010 | Configuration errors | 7 | 6 | 5 | 210 | High | Startup sanity gate; config baselines |
| ORK-011 | Software upgrade failure | 8 | 5 | 6 | 240 | Critical | Pre-upgrade backup; both-provider rehearsal |
| ORK-012 | Backup and recovery failure | 9 | 5 | 8 | 360 | Critical¹ | §6 backup/restore design with drills |
| ORK-013 | Network partitioning | 6 | 4 | 5 | 120 | High | Per-segment probes; staleness alerting |
| ORK-014 | Notification delivery failure | 6 | 6 | 7 | 252 | Critical¹ | Outcome record + retry; independent watchdog |
| ORK-015 | Secrets / credential rotation failure | 8 | 4 | 7 | 224 | Critical¹ | Secret inventory; rotation runbooks |
| ORK-016 | Dependency vulnerability exposure | 7 | 5 | 4 | 140 | High | Full-solution CI scanning; SBOM; patch blockers |

¹ Band reached or raised via the §2.5 overrides (S≥9, or S≥7 with D≥7).

Aggregate observation (ANALYSIS, INFERRED): the dominant pattern across
Critical/High entries is **silent failure** (notification swallowing, absence
of post-expiry alerting, absence of backup tooling) combined with
**documented-but-unverified external interoperability** (ADCS/EJBCA,
PostgreSQL upgrades). The highest-leverage PROPOSED controls are therefore
detection investments (§5) and the recovery design (§6), ahead of new
functional capability.

## 5. Monitoring and observability recommendations

AS-IS observability (with provenance):

| Mechanism | What it covers | Provenance |
|---|---|---|
| `GET /api/status/health` with `database` / `ca_connectors` components | Local process and dependency reachability | DOCUMENTED (`docs/DEPLOYMENT.md`); CODE-OBSERVED healthcheck in `docker-compose.yml` |
| Serilog structured logging (console + rolling daily files; correlation/client-IP enrichers per ARCHITECTURE) | Enrollment, validation failures, watcher scans, dispatch failures | DOCUMENTED; CODE-OBSERVED |
| Enrollment events (persisted, listed via `/api/status/enrollments`) | Per-attempt outcomes incl. rejection reasons | DOCUMENTED; CODE-OBSERVED |
| Certificate expiry watcher log lines | Watcher start/stop, scan failures, notified counts | CODE-OBSERVED |
| Backend **Test** action | Point-in-time connector reachability | DOCUMENTED |
| Dashboard certificate/device/event listings | Manual inspection | DOCUMENTED |
| CI build/test/audit gates (UI advisory gate) | Build-time quality, not runtime | VERIFIED (tracker) |

Gaps (INFERRED from absence unless noted): no metrics endpoint
(`docs/DEPLOYMENT.md` marks Prometheus integration as future); no external
synthetic probing; no alert on watcher scan failure beyond logs (the watcher
catches scan exceptions and continues — CODE-OBSERVED — so repeated scan
failure is silent); no notification-outcome record; no clock-drift or
CRL-staleness checks.

PROPOSED observability plan (all items [PROPOSED], requiring stakeholder
approval):

1. **Metrics endpoint** (Prometheus format): enrollment counts by
   profile/status, renewal success/failure by device, revocation attempts and
   outcomes, CRL `nextUpdate` staleness per issuer, watcher scan age/last
   success, SMTP dispatch outcomes, CA connector latency and error rate,
   certificate-expiry horizon distribution.
2. **Alerts** (proposed minimum set):
   - Watcher scan failing, or last successful scan older than 48 h.
   - Any certificate crossing into expired state.
   - Revocation request ending in 5xx/501.
   - Notification dispatch failure streak (≥ 3 consecutive).
   - Health endpoint loss of a component.
   - CRL `nextUpdate` within threshold of lapse.
3. **Synthetic checks**: periodic device-like `/cacerts` GET, CRL GET/HEAD
   over the real listeners, and (in approved environments) a full enrollment
   probe.
4. **Log retention and review**: align rolling-file retention with incident
   needs; include logs in backup scope (§6).
5. **Dashboards**: gateway availability; credential-lifecycle health
   (certificates approaching expiry vs. renewals completed); revocation
   coverage.

These recommendations are inputs to KSPEC-003 (SR-P/SR-R/SR-V/SR-U areas) and
KSPEC-008 (VT entries); they are not normative until approved.

## 6. Backup, restore, and recovery design

### 6.1 AS-IS position

No backup or restore capability is implemented in the baseline for gateway
state (see ORK-012). Operator guidance is limited to documentation:

- Windows package: "Back up database, CA and encryption keys together before
  upgrades"; "Do not treat MSI rollback as a database rollback; schema-upgrade
  testing is required" (`docs/windows-installation.md`, DOCUMENTED).
- Uninstall retains `C:\ProgramData\KryptonianSandbox` private state
  (DOCUMENTED).
- The compose deployment has a `postgres_data` named volume but no backup job
  (CODE-OBSERVED).
- The specific Azure plan relies on platform point-in-time restore
  (DOCUMENTED, deployment-specific).

### 6.2 PROPOSED backup design (TO-BE/PROPOSED — requires approval)

**Backup set (completeness rule):** a backup is only restorable if it captures,
from the same point in time and the same host:

1. Database (SQLite file(s) under the private data directory, copied with the
   service stopped or via a provider-consistent snapshot; `pg_dump` /
   platform snapshot for PostgreSQL).
2. CA material: CA certificate + private key (PFX or PEM pair) for every
   configured backend, plus the CA PFX passwords.
3. ASP.NET Data Protection key ring (required to decrypt the stored SMTP
   password; loss breaks notification delivery after restore — INFERRED from
   Data Protection semantics, `DataProtectionService` CODE-OBSERVED).
4. Gateway configuration actually in effect (environment variables, listener
   configuration, installed-version identifier).
5. (Optional, retention-permitting) enrollment events and logs for forensics.

**Schedule and retention (TBD — recommendation only):**

| Item | Recommendation | Rationale |
|---|---|---|
| Database | Daily full (stopped-service SQLite copy, or `pg_dump`) | Minimizes RPO for lifecycle state |
| CA material + key ring | On change, verified quarterly | Changes rarely; loss is catastrophic |
| Retention | ≥ 30 days daily, ≥ 12 monthly | TBD by stakeholders |
| Storage | Off-host, access-controlled, encrypted; never the same volume as live state | Avoids a single failure domain |
| Verification | Restore rehearsal into a clean environment at a defined cadence (recommendation: quarterly) and before every upgrade | An unverified backup is not a backup |

**Restore procedure (PROPOSED skeleton):**

1. Freeze writes (stop gateway service/containers).
2. Capture a pre-restore state snapshot for forensics.
3. Restore the Data Protection key ring first, then CA material with original
   passwords, then the database.
4. Start the gateway so `SchemaUpgrades` runs against the restored database;
   confirm additive upgrades are idempotent (CODE-OBSERVED idempotent shapes:
   `ADD COLUMN IF NOT EXISTS`, `CREATE TABLE IF NOT EXISTS`,
   `PRAGMA table_info` guarded).
5. Verify: health endpoint all components healthy; enrollment via a test
   device; CRL served per issuer; SMTP test email; revocation round-trip.
6. Resume service and record the drill outcome.

**Recovery objectives:** RPO/RTO targets are **TBD** and require stakeholder
decision; the recommendation above bounds RPO at 24 h for lifecycle state.

**Recovery priority when only partial material exists (PROPOSED guidance):**
CA key material is the least recoverable asset — its loss forces full device
re-enrollment and breaks revocation for previously issued certificates
(ORK-008, ORK-015); the database is reconstructable by re-provisioning every
device. Backup completeness must therefore prioritize CA keys and the Data
Protection key ring.

## 7. Traceability and open questions

### 7.1 Requirement linkage (area-level; concrete bindings in KSPEC-009 §7.2)

Per §2.6, the following prefix-level linkage intent is recorded for KSPEC-009
to bind once the Requirements Register exists. This document defines no
requirements.

| ORK | Target requirement areas (KSPEC-003 prefixes) |
|---|---|
| ORK-001, ORK-007 | SR-C (certificate lifecycle), SR-R (reliability), SR-X (failure/recovery) |
| ORK-002, ORK-013 | SR-V (availability), SR-F-INT (interoperability), SR-R |
| ORK-003, ORK-008 | SR-C, SR-F-CAI, SR-X |
| ORK-004, ORK-005 | SR-A (authentication), SR-Z (authorization), SR-S (security), SR-C |
| ORK-006, ORK-012 | SR-D (data management), SR-X, SR-O |
| ORK-009 | SR-R, SR-C, SR-D |
| ORK-010, ORK-011 | SR-B (deployment), SR-S, SR-O, SR-X |
| ORK-014 | SR-O, SR-R, SR-U (audit) |
| ORK-015, ORK-016 | SR-S, SR-A, SR-O, SR-B |

### 7.2 Open questions and TBD items (not fabricated)

| # | Open question | Owner decision needed |
|---|---|---|
| TBD-1 | Production CRL `nextUpdate` lifetime (AS-IS prototype value: 60 s, CODE-OBSERVED) | Stakeholders |
| TBD-2 | Backup RPO/RTO targets and retention (§6.2) | Stakeholders |
| TBD-3 | Out-of-application renewal mechanism and policy (ORK-001) | Stakeholders |
| TBD-4 | ADCS/EJBCA production interoperability verification plan (ORK-003, ORK-008) | Deployment/interoperability track (also KSPEC-010) |
| TBD-5 | PostgreSQL persistence exercise plan (ORK-011) | Deployment track |
| TBD-6 | Remediation plan for recorded transitive advisories (ORK-016) | Engineering/deployment track |

### 7.3 Risks to other documents

- KSPEC-006 (cybersecurity): ORK-005 and ORK-004 overlap threat modeling;
  boundary agreed here is *operational failure/misconfiguration* vs.
  *deliberate adversarial action*.
- KSPEC-010 (gap analysis): ORK-011, ORK-012, ORK-016 constitute evidence for
  GAP entries; identifiers to be cross-linked when KSPEC-010 is written.
- KSPEC-008 (V&V): verification methods in §3 map to VT candidates.

## 8. Provenance summary and evidence index

Provenance classes per KSPEC-000 §5. Summary of this document's assertions:

| Class | Count (approx.) | Representative assertions |
|---|---|---|
| VERIFIED | 12 | Activation replay rejection; renewal identity preservation; forwarding-spoof rejection; restart persistence; proxy trust checks incl. fail-closed expired PFX; online revocation demonstration; CI gates; MailKit patch; DateTime-kind regression fix |
| CODE-OBSERVED | 30+ | Watcher behavior; dispatcher swallow; connector error paths; fail-closed revocation; schema upgrades; compose defaults and absence of backup; Data Protection usage; CRL 60 s lifetime; rotated-CA guard |
| DOCUMENTED | 15+ | Tracker boundaries; deployment checklist; notification model; CA backend configuration; installer behavior and caveats; Azure plan backups |
| INFERRED | 10 | Absence of metrics/synthetic monitoring/backup tooling/secret-cadence tracking; Data Protection key-ring loss effects |
| UNKNOWN | 2 | Real hospital CA failure behavior (ADCS/EJBCA field behavior); production load behavior |
| PROPOSED | All §3 PROPOSED controls, §5 observability plan, §6.2 backup design | Require stakeholder approval; not normative |

Primary evidence files relied upon (all at baseline commit
`682f996c8447afec13bc1fce5b7f59e6a06efa25`):

- `src/RTSec.Kryptonian.Api/HostedServices/CertificateExpiryWatcher.cs`
- `src/RTSec.Kryptonian.Api/Controllers/EstController.cs`
- `src/RTSec.Kryptonian.Api/Controllers/NotificationSettingsController.cs`
- `src/RTSec.Kryptonian.Application/Notifications/NotificationDispatcher.cs`
- `src/RTSec.Kryptonian.Application/Services/CertificateRevocationService.cs`
- `src/RTSec.Kryptonian.Infrastructure/Harness/AdcsCaConnector.cs`
- `src/RTSec.Kryptonian.Infrastructure/Harness/EjbcaCaConnector.cs`
- `src/RTSec.Kryptonian.Infrastructure/Acme/AcmeCaConnector.cs`
- `src/RTSec.Kryptonian.Infrastructure/Notifications/MailKitSmtpSender.cs`
- `src/RTSec.Kryptonian.Infrastructure/Data/SchemaUpgrades.cs`
- `src/RTSec.Kryptonian.Infrastructure/Data/UnitOfWork.cs`
- `docker-compose.yml`; `deploy/`; `installer/windows/`
- `docs/IMPLEMENTATION-TRACKER.md`, `docs/DEPLOYMENT.md`, `docs/ARCHITECTURE.md`,
  `docs/device-enrollment.md`, `docs/ca-backend-configuration.md`,
  `docs/email-notifications.md`, `docs/windows-installation.md`,
  `docs/windows-sandbox.md`, `docs/adcs-real-backend-demo.md`,
  `docs/sequence-diagrams/README.md`, `plans/azure-deployment.md`

### 8.1 Methodology limitations

- Ratings are qualitative anchor-based judgments by the documentation team;
  they have not been calibrated against incident data (none exists in the
  repository) and carry no statistical meaning.
- No failure probabilities, MTBF, or availability percentages are asserted;
  none can be derived from the repository at this baseline.
- Executed-test evidence cited is local developer evidence recorded in
  `docs/IMPLEMENTATION-TRACKER.md` and `artifacts/developer/verify-*/`; it is
  not conformance or certification evidence, and the tracker states this
  explicitly.
- Presence/absence claims ("no automated backup", "no metrics endpoint")
  derive from a repository inspection at the baseline commit; if additional
  mechanisms exist outside the repository, this assessment must be revised.

---

*End of KSPEC-007 — Draft, pending review. All PROPOSED items require explicit
review and approval before becoming normative (KSPEC-000 §4, §5).*
