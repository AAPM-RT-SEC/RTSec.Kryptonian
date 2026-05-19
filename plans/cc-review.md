# Review: cc-finalize-kryptonian.md

**Reviewer:** Claude (Opus 4.7)
**Date:** 2026-05-19
**Subject:** Codex plan to finalize the Kryptonian contestant gateway for the AAPM RT-SEC 2026 hackathon

## Verdict

**Approve with changes.** The plan's framing is correct — device-facing EST, CA-backend-agnostic from the device perspective, gateway internally routes to the active connector, contestant-side DICOM bridge as a separate worker. That matches both the existing architecture in `src/` and the spirit of the hackathon brief. However, several scoring-critical details are missing and one design choice (single active backend) creates real friction with how the leaderboard awards points. Address the items in [§3 Required Changes](#3-required-changes) before implementation.

---

## 1. What the Plan Gets Right

- **Architectural alignment.** The existing code already separates concerns the way the plan describes: `EnrollmentOrchestrator` resolves a backend via `EstProfile` and dispatches to an `ICaConnector` ([EnrollmentOrchestrator.cs:60](src/RTSec.Kryptonian.Application/Services/EnrollmentOrchestrator.cs:60), [ICaConnector.cs](src/RTSec.Kryptonian.Domain/Interfaces/ICaConnector.cs)). Adding a "Device" aggregate above the orchestrator is incremental, not a rewrite.
- **Read-only harness boundary.** Explicitly fencing `RTSec.Kryptonian.CaHarness` off from this work is correct — the deployed harness is the acceptance system per [HACKATHON-DELIVERABLE.md:19](docs/HACKATHON-DELIVERABLE.md:19).
- **EST as the device surface.** Per-backend protocols (EST/SCEP/EJBCA-REST/ACME) are *upstream* concerns; the device only speaks EST. This is medically defensible and matches RFC 7030's intent.
- **ACME path preserved.** Keeping the validated HTTP-01 relay (recent commits `664fb52`, `5679158`, `b09e8d8`) in place is the right call — it was hard-won.
- **DICOM bridge isolated as a worker.** Separating Flow 2/3 from the gateway HTTP surface is clean and matches fo-dicom's threading model.

## 2. Strengths-but-Underspecified

These read as intentions; they need to land as concrete contracts before code starts.

### 2.1 Device-to-CSR binding
The plan says enrollment is blocked for "unknown" devices, but the EST `simpleenroll` endpoint today ([EstController.cs:108](src/RTSec.Kryptonian.Api/Controllers/EstController.cs:108)) only sees an unauthenticated PKCS#10 CSR. **How does the gateway map an inbound CSR to a registered Device row?** Options:
- CSR Subject DN / CN exact match against `Device.SubjectDn` (simple, demo-friendly).
- Pre-shared enrollment token issued at "approve" time and passed as an HTTP header.
- TLS client cert + `simplereenroll` for renewals only; initial enroll is admin-driven from the UI.

Pick one and write it down. Without this, the "block unknown/pending/removed" requirement is not implementable.

### 2.2 Single-active-backend vs. four-score-columns
The hackathon awards Flow 1 points **per backend** ([HACKATHON-DELIVERABLE.md:170-178](docs/HACKATHON-DELIVERABLE.md:170)). The plan's single-active-backend model means earning the full Flow 1 sweep requires the operator to: activate `selfsigned` → demo-enroll → C-STORE to 4243 → switch to `adcs` → demo-enroll → C-STORE → repeat. That's defensible as an admin UX, but the plan should:
- Spell out that this flip-flop is the intended demo flow.
- Confirm that previously-issued certs from a now-inactive backend remain usable for Flow 1's mTLS C-STORE (i.e., the cert outlives the active-backend toggle).
- Decide whether the UI shows a per-backend "last successful enrollment" indicator so judges can see four green checks at once.

### 2.3 Active-backend ↔ EstProfile mapping
Today, the orchestrator resolves a `CaBackend` via `profile.CaBackendId`. The plan introduces a global "active backend" but does not say how it interacts with `EstProfile.CaBackendId`. Two coherent options:
- Demote `EstProfile` to a routing-only entity and have the orchestrator override `profile.CaBackendId` with the active backend at enrollment time.
- Keep `EstProfile` per-backend, but have a single hostname/path the device always uses, and have the EST profile resolver pick whichever profile points to the active backend.

The plan should pick one and explicitly state what happens to `EstProfile.CaBackendId` so the implementer doesn't invent a third option mid-build.

### 2.4 Flow 2 cert binding (`X-Device-Certificate`)
[HACKATHON-DELIVERABLE.md:104-106](docs/HACKATHON-DELIVERABLE.md:104) requires the bridge's STOW request to carry `X-Device-Certificate: <certificateDerBase64 from your enrollment>` and `X-Transfer-Mode: cmove`. The plan describes Flow 2 mechanically but never says **which cert** the bridge picks or how the bridge gets it from the gateway. Add: "Bridge resolves the active backend's most-recent EST-enrolled cert by serial number from the gateway DB (or via an internal API) before issuing STOW."

### 2.5 ACME claim wiring
The hackathon requires `POST /teams/{token}/api/backends/acme/claim` with the cert PEM ([HACKATHON-DELIVERABLE.md:80](docs/HACKATHON-DELIVERABLE.md:80)) — the DIMSE proxy at 4243 cannot validate step-ca-issued certs. The plan says "ACME remains an upstream CA connector path" but does not name the claim step. Add: "After a successful ACME enrollment, the demo flow POSTs the cert PEM to `/teams/{token}/api/backends/acme/claim` and surfaces the scoreboard response in the UI."

### 2.6 OID validation
Hackathon explicitly states gateway-credit certs carry per-backend OIDs (`1.3.6.1.4.1.99999.{1,2,3}`) ([HACKATHON-DELIVERABLE.md:158-164](docs/HACKATHON-DELIVERABLE.md:158)). The plan doesn't mention asserting these OIDs on stored certs. A 10-line check in `EnrollmentOrchestrator` (after `IssueCertificateAsync` returns) that records the discovered OID on `Certificate` would (a) prove the cert actually came from the gateway path, not `/issue`, and (b) give the UI something to show judges. Add to test plan as well.

### 2.7 DIMSE 4243 trust-anchor handling
The bridge/gateway needs to trust the proxy server cert downloaded from `http://kryptonian-dimse.eastus.cloudapp.azure.com:8044/server-cert` ([HACKATHON-DELIVERABLE.md:82-86](docs/HACKATHON-DELIVERABLE.md:82)). The plan doesn't address: who fetches the PEM, where it's stored, and which fo-dicom `TlsClientOptions` consumes it. Either pin this in config, or add a startup hook that bootstraps the trust store.

### 2.8 `RTSec.Kryptonian.DimseTlsProxy` role
There is already a `DimseTlsProxy` project in the solution. The plan never addresses it. Is it being deleted, left dormant, or repurposed? State it explicitly — otherwise the implementer may waste time wiring it into the contestant gateway when in fact the proxy is harness-side and contestants only *connect* to it.

## 3. Required Changes

Before implementation kickoff the plan needs:

1. **Device identity contract** — name the field used to bind CSR → Device (see §2.1).
2. **Active-backend persistence model** — say where it lives (new column on a singleton row? `EstProfile.IsActive` flag with a uniqueness constraint? `AppSettings`?) and how it interacts with `EstProfile.CaBackendId` (§2.3).
3. **Concrete admin API for activation** — beyond `POST /api/cas/{id}/activate`, define the read model: `GET /api/cas/active` or a field on the list response.
4. **Demo enrollment API shape** — e.g., `POST /api/devices/{id}/demo-enroll` returning the issued cert PEM + observed OID + harness-side scoring response. Mentioned in plan but not specified.
5. **Bridge → gateway integration** — internal contract for fetching the most-recent enrolled cert (DER + private key for Flow 2 STOW, just DER for header). §2.4.
6. **ACME claim step in the demo flow** — §2.5.
7. **OID assertion** — §2.6, plus a test in the unit-test list.
8. **DIMSE proxy trust bootstrap** — §2.7.
9. **DimseTlsProxy disposition** — §2.8.
10. **Configurable team-token surface** — plan mentions "team token" as bridge-configurable; this needs to be a gateway-wide setting (Devices, ACME, bridge, and Flow 3 claim all need it). Add to `appsettings.json` + admin UI.

## 4. Test-Plan Gaps

Add to the test plan:

- **OID extension assertion** on stored certs (unit).
- **DICOMweb WADO multipart boundary edge cases** — the plan mentions parsing but not malformed boundaries, missing `Content-Type`, or split chunks.
- **Active-backend toggle race** — two near-simultaneous enrollments during a switch must not partially route.
- **Device-removed → re-enroll attempt** must produce an `EnrollmentEvent` with a `Rejected` status and the rejection reason, not just a 4xx.
- **Live smoke: end-to-end leaderboard verification** — record the team token and final scoreboard JSON snapshot as a build artifact, per [HACKATHON-DELIVERABLE.md:36](docs/HACKATHON-DELIVERABLE.md:36).

## 5. Minor / Nits

- Plan refers to "EST-facing gateway surface" — fine, but the existing controller is at `/.well-known/est` ([EstController.cs:13](src/RTSec.Kryptonian.Api/Controllers/EstController.cs:13)). Restating the route avoids confusion.
- "Demo key generation/storage is acceptable for the showcase" — agree, but the plan should still name where keys live (in-memory? `IDataProtectionService`-wrapped column on `Certificate`?). Without this the bridge can't perform Flow 1's mTLS handshake.
- The plan does not mention `/api/devices/{id}/certificates` semantics for **renewal** vs. **history**. Decide whether it returns all certs ever issued or just the active one per backend.
- Recent commit `6d9a5eb` made Orthanc verification best-effort in Flow 3 scoring on the harness side. Bridge implementation should still treat a 200 from the claim endpoint as authoritative and not over-engineer client-side verification.

## 6. Risk Register

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Device-CSR binding ambiguity delays demo | High | High | Lock §2.1 before coding |
| Active-backend UX confuses judges during live demo | Medium | Medium | Per-backend "last enrolled" indicators + scripted demo |
| OID validation gap causes silent scoring loss | Medium | High | §2.6, plus a smoke-test that asserts the OID is present before claiming |
| DIMSE 4243 trust misconfiguration on demo day | Medium | High | Bootstrap PEM at startup, log the SHA-256 to console |
| fo-dicom DICOMweb multipart parser quirks | Medium | Medium | Use a known-good MIME parser; don't hand-roll boundaries |
| Forgetting the ACME claim POST | High | Medium | Hard-wire the claim into the ACME demo button, not optional |

## 7. Recommendation

Have Codex revise the plan to incorporate §3 items (especially 1, 2, 5, 6, 7), then proceed to implementation. The architecture is sound; the gaps are specification-level, not design-level. Estimated 30-60 minutes of plan editing avoids 4-8 hours of mid-build rework.
