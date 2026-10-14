# Kryptonian Code and Document Reconciliation

| Field | Value |
|---|---|
| Document ID | KSPEC-016 |
| Title | Code, Documentation and Specification Reconciliation |
| Version | 0.2 (Draft) |
| Status | Draft - technical reconciliation completed; stakeholder approval pending |
| Created | 2026-10-13 |
| Baseline commit | `682f996c8447afec13bc1fce5b7f59e6a06efa25` |
| Revision history | 0.2 - source audit, specification corrections and reproducible PDF publication |
| Source references | Source paths below; KSPEC-003 canonical register; KSPEC-008 verification records; validation files |
| Author | Codex - technical reconciliation, 2026-10-13; no stakeholder signature |
| Reviewer | Unassigned |
| Approver | Unassigned - no approval recorded |

## 1. Assessment and scope

The main EST, profile routing, issuance-policy, renewal, local CRL and device-management
flows are consistent with the baseline source. The original specification also contains
overstatements of implementation completeness, inferred test coverage and deployment
architecture. This revision corrects those claims without changing application code or
weakening the target requirements to accommodate defects.

This is a source and documentation audit with selected automated execution. It is not
an exhaustive proof of every requirement, a current vulnerability audit, a standards
conformity assessment or product approval. Historical developer evidence remains
historical. Test definitions alone are CODE-OBSERVED; the fresh run record identifies
only what was actually executed in this review.

Markdown in this directory is authoritative editable source. The PDF is a production
publication artifact of the Draft specification; publication does not promote any
requirement to Approved. The baseline SHA identifies application source. The publication
manifest separately hashes all Markdown sources, so uncommitted specification revisions
can be identified precisely.

## 2. Reconciliation findings and applied changes

| Finding | Source observation | Specification disposition |
|---|---|---|
| RF-014 - activation timing | `EnrollmentOrchestrator.EnrollAsync` validates bootstrap and CSR signature, then saves activation consumption before resolving the backend, enforcing issuance policy and calling the CA. | UR-002, SR-F-REG.002, VT-015 and handoff corrected. Issuance is not atomic with consumption; failure/pending/crash recovery is GAP-039 and VT-060. |
| RF-015 - issuer routing | `GetCaCertsAsync`, `IssueForDeviceAsync` and `ResolveProfileBackendAsync` use `profile.CaBackendId`; the global active backend is a dashboard concept. | UR-005, SR-F-CAI.002 and VT-040 now specify profile-bound enabled backend routing. |
| RF-016 - audit completeness | Early orchestrator failures return without creating an EnrollmentEvent; controller failures and `CertificateRevocationService` do not uniformly persist lifecycle decision events. | SR-C.010, SR-U.001 and SR-U.002 reclassified Existing to Partial. Target completeness retained; GAP-038 and VT-059 added. |
| RF-017 - performance evidence | `IpRateLimiting.GeneralRules` specifies 100/min general and 30/min for labeled enrollment POST patterns. Configuration is not a throughput benchmark. Unlabeled rule matching has no fresh proof. | SR-P.001 reclassified Partial. Limits, effective-rule testing and client-IP attribution clarified in SR-S.006 and ICD. |
| RF-018 - notification timing | `CertificateExpiryWatcher` uses fixed 24-hour scan, 2-minute startup delay and 7-day repeat interval. Renewal suppression checks a different LastCertificateId after newer issuance. | SR-F-CLM.005 reclassified Partial; fixed timing and successful-issuance suppression documented. VT-029 reverse-matrix evidence corrected; configurable repeat timing remains a target. |
| RF-019 - anonymous surface | Auth status/setup/login/logout, CRL GET, EST, challenge handlers, health map and dashboard assets do not all require framework authorization. EST handlers perform their own credential checks. | SR-Z.008 enumerates the actual surface; anonymous framework routing does not mean unauthenticated enrollment. |
| RF-020 - portal architecture | API `Program.cs` serves React static files from wwwroot/ui or the adjacent UI dist. Web `Program.cs` starts a separate Blazor/MudBlazor portal. | Product definition, conventions, design and summary corrected; React is not embedded in Web. |
| RF-021 - certificate versus device withdrawal | `CertificateRevocationService.RevokeAsync` mutates certificate/CRL state, not Device.Status. Renewal rejects the revoked credential; other valid device credentials are not automatically withdrawn. | SR-F-CLM.007 distinguishes certificate revocation from device removal. |
| RF-022 - incomplete requirement records | SR-F-ADM.005, SR-F-ADM.010, SR-A.004, SR-D.006 and SR-B.005 lacked Evidence and Approval fields. | Mandatory fields added, with CODE-OBSERVED evidence and Review Required approval. |
| RF-023 - identifier and review integration | KSPEC-003 exists, but design/risk/handoff claimed it or later documents were absent; SR-I and SR-C-CLM were not allocated prefixes; UR-007 had no upstream edge despite existing setup/last-admin requirements. | Current integration text corrected; allocated interoperability prefix used; UR-007 bound to SR-F-ADM.003/.005. Historical review findings retained and current dispositions recorded. |
| RF-024 - standards vocabulary | RFC 7030 uses application/pkcs10 and application/pkcs7-mime. Basic credential provisioning is site-specific; 202 pending is part of EST. | Removed false media-type/Basic/pending deviation implications and Conformant labels. No whole-protocol conformity claim introduced. |

All findings RF-014 through RF-024 are closed as specification corrections. The
underlying implementation limitations remain open gaps, not fixed code defects.

Publication inspection also corrected sequence-label punctuation and the handoff's
state-diagram grouping syntax. The certificate diagram now identifies expiration as
logical validity: NotAfter is checked, but no baseline job writing the Expired enum
value was observed. Diagram notes no longer claim complete rejection auditing.

## 3. Source evidence map and important limits

| Area | Inspected evidence | Evidence limit |
|---|---|---|
| API and transport | `src/RTSec.Kryptonian.Api/Program.cs`; all 15 API controllers; `Authentication/ConfiguredClientCertificateTrust.cs`; `Middleware/CertificateForwardingGuard.cs` | Configuration, authorization attributes and pipeline ordering observed; no live deployment/network-capacity claim. |
| Enrollment and renewal | `src/RTSec.Kryptonian.Application/Services/EnrollmentOrchestrator.cs`; `EnrollmentCertificatePolicy.cs`; `DeviceService.cs`; `src/RTSec.Kryptonian.Infrastructure/Data/UnitOfWork.cs` | Ordinary success, failure and concurrency paths inspected; external CA/crash recovery remains unverified. |
| Revocation | `CertificateRevocationService.cs`; `CrlController.cs`; `ICaConnector.cs`; local and remote connectors | Gateway administrative revocation requires GenerateCrlAsync. EJBCA's separate RevokeCertificateAsync method is not invoked by that service; it is not proof of supported gateway revocation. |
| EJBCA chain caching | `EjbcaCaConnector.GetCaCertificatesAsync`; `CaConnectorFactory`; factory registration in API Program.cs | Chain cache is per connector instance. Factory is scoped; a fresh request can see an empty chain even after an earlier issuance. “Empty until first issuance” must not imply durable/global caching. |
| Renewal authorization | `EnrollmentOrchestrator.ReenrollAsync`; device client renewal scheduling | Gateway checks validity, stored certificate, profile, device and identity. It does not enforce a configured early-renewal time window. Client scheduling is a separate policy. |
| Notifications | `CertificateExpiryWatcher.cs`; `NotificationDispatcher.cs`; `MailKitSmtpSender.cs`; existing dispatcher/settings test definitions | Fresh SMTP delivery and watcher fault/timing behavior were not executed. |
| Persistence | EF configurations, schema upgrades, UnitOfWork; test definitions under tests | SQLite historical persistence evidence exists; hospital PostgreSQL exercise remains unverified. |
| Deployment/UI | Both API and Web Program.cs; installer scripts; Docker Compose; README; BUILD; deployment/architecture/device/user docs; implementation tracker | Older docs are historical descriptions where code contradicts them. Default appsettings port keys do not themselves create Kestrel listeners; sandbox explicitly creates loopback listeners. |
| Contracts | `OpenAPI.Admin.yaml`, `OpenAPI.Client.yaml`, `OpenAPI.ESTProfiles.yaml`, `OpenAPI.Schema.yaml`, `OpenAPI.StatusObserve.yaml`; controller inventory | Client file is empty; source/API drift remains GAP-029. PDF publication does not repair machine-readable contracts. |
| Standards terminology | [RFC 7030](https://www.rfc-editor.org/rfc/rfc7030.html), sections 3.2.3, 4.2.1 and 4.2.3 | Media-type, Basic provisioning and pending-response terminology checked only; no complete standards evaluation. |

Dependency advisories in the original package come from the September implementation
tracker. They are historical findings requiring re-audit, not a fresh October security
assessment. Likewise, no organizational legal or regulatory classification decision is
recorded by this engineering review; the package's nonclinical product scope is retained.

## 4. Verification and publication records

The existing API test project was restored and run after the initial no-restore attempt
failed with NETSDK1127 (missing targeting pack): **72 passed, zero failed, zero skipped**.
The TRX record is under `validation/test-results/`. Compiler/analyzer warnings were
present. This run does not establish SMTP delivery, external CA interoperability,
PostgreSQL persistence, production throughput or the new proposed acceptance tests.

The subsequent solution run completed across all eight test assemblies:
**337 passed, zero failed, 18 skipped, 355 total**. The 18 existing infrastructure
skips remain unverified behaviors. The separate 72-test API run is not added again
to those totals. See `validation/verification.json` for per-assembly counts and TRX files.

The broader run, mechanical specification checks, source/PDF hashes and PDF visual-QA
results are recorded in `validation/verification.json` and
`validation/publication-manifest.json`. Generated test logs and TRX records are local
validation outputs and are ignored by version control. Re-run the commands in the
package README to reproduce rather than accepting a screenshot as test evidence.

## 5. Approval and implementation follow-up

The 20 decision records remain stakeholder decisions. Performance, availability,
retention, CRL cadence, secrets provisioning and partner profile choices are not filled
in by inference. Revised Existing/Partial/Missing/Proposed states describe the evidence
available here; they do not constitute requirement approval.

Prioritize complete durable audit coverage (GAP-038), consumed-bootstrap recovery
(GAP-039), trusted client-IP attribution (GAP-040), and the existing security/operational
deployment blockers. Independent verification against the target requirements remains
necessary before production deployment.
