# Kryptonian Formal Engineering Specification — Independent Review Findings

| Field | Value |
|---|---|
| Document title | Independent Review Findings (RF-nnn) |
| Document ID | KSPEC-013 |
| Version | 0.2 (Draft) |
| Review status | Draft — pending review |
| Created | 2026-10-13 |
| Baseline repository commit | `682f996c8447afec13bc1fce5b7f59e6a06efa25` (branch `feat/windows-sandbox-installer`, "Release Windows gateway and portable enrollment demo 0.1.4") |
| Repository | https://github.com/AAPM-RT-SEC/RTSec.Kryptonian |
| Reviewed material | KSPEC-000 – KSPEC-012 in `docs/formal-specification/` (13 documents) |
| Revision history | 0.1 — 2026-10-13 — Initial independent review of the full package (Author: independent engineering reviewer). |
| Source references | KSPEC-000 (conventions and controls); all reviewed documents; repository evidence (docs/IMPLEMENTATION-TRACKER.md, src/RTSec.Kryptonian.Api/Controllers/, tests/, artifacts/developer/verify-*/, solution file) |
| Author | Name: TBD — Date: TBD — Signature: TBD |
| Reviewer | Name: TBD — Date: TBD — Signature: TBD |
| Approver | Name: TBD — Date: TBD — Signature: TBD |

---

**Current status:** the original review below is retained as history. All RF-001 through RF-024 documentation findings are dispositioned by revision 0.2; consult the final current-disposition section and KSPEC-016. Implementation gaps and stakeholder approval remain open.

## 1. Review scope and method

All 13 generated documents (00–12) were read in full. Structured checks performed:

- **Header controls**: document ID, version, review status, creation date, baseline
  commit, revision history, source references, and author/reviewer/approver
  placeholders present on every document. Baseline commit `682f996c…` is stated
  consistently across all documents (04/05 abbreviate the header but cite the same SHA).
- **Identifier scheme**: UR (38), SR (154), THR (18), ORK (16), VT (58), GAP (37)
  populations recounted programmatically against the owning documents.
- **Provenance/status discipline**: no approval is inferred from implementation; all
  Approval fields are "Review Required"; no FDA/IEC 62304/ISO 14971/medical-device
  regulatory content found; conformity disclaimers present in 02/03/05/06/08/09/10/11.
- **Repository spot-checks**: 23 cited documentation paths, 10 cited test files
  (`EstControllerTests.cs`, `EstRouteTests.cs`, `DeviceActivationConcurrencyTests.cs`,
  `CertificateForwardingGuardTests.cs`, `ConfiguredClientCertificateTrustTests.cs`,
  `SandboxBootstrapTests.cs`, and others), the controller inventory, the solution's
  test-project list, `artifacts/developer/verify-fa867498…/evidence.json`, the
  rate-limit configuration (100/30/30), the 15-minute default activation-code
  lifetime, and the 24 h / 7-day / 2-min watcher constants were all verified against
  the actual repository. **All spot-checked citations were accurate.**
- **Encoding**: every file scanned for double-encoded UTF-8 (mojibake).

Positive observations worth recording: the 09 status distribution (Existing 113 /
Partial 20 / Missing 18 / Proposed 3) matches the KSPEC-003 register exactly; the
VT→SR edges sampled (VT-011/012/044 etc.) match the KSPEC-008 "Linked requirements"
fields; the ORK RPN arithmetic in 07 §4 was recomputed and is correct; THR/ORK/VT/GAP
counts match their headers. Evidence fidelity across the package is high and no
fabricated evidence, test result, approval, or conformity claim was detected.

The deficiencies found are concentrated in **cross-document reconciliation** (a
byproduct of parallel authoring), two **factual miscounts**, and a small number of
**wording-discipline** items.

---

## 2. Findings

### RF-001 — Stale "document does not yet exist" statements and prefix-only requirement references contradict the delivered package

- **Affected**: KSPEC-004 §1.3, §18; KSPEC-005 preamble, §19; KSPEC-007 §2.6;
  KSPEC-012 header, §1.2, §9, §13.2; inconsistent with KSPEC-003 §22 and KSPEC-008 §8.
- **Severity**: Major
- **Evidence**: 04 §1.3 states "At the time of writing, `03-SOFTWARE-REQUIREMENTS.md`
  (KSPEC-003) and its requirements register do **not yet exist**"; 05 states the same
  and restricts itself to prefix-level references (`SR-EST`, `SR-I`…); 07 §2.6 states
  the register "is not yet present"; 12 states KSPEC-008/009/010/011 were "not yet
  present" when drafted. Yet KSPEC-003 exists with a canonical 154-SR register and
  asserts (03 §22) that downstream documents "reference these SRs by ID only", and
  09 resolves concrete SR IDs. Simultaneously, 12 cites concrete SR IDs (e.g.,
  SR-F-REG.006, SR-S.011) while declaring 008–011 unpublished, and 07 §7.1 targets an
  `SR-I` prefix that 03 never allocated (interoperability lives under `SR-F-INT`) —
  a mismatch 09 §9.4 item 6 already reports but cannot resolve.
- **Impact**: Requirement references in 04/05/07 cannot be machine-traced; 03 §22's
  downstream-reference claim is false as written; reviewers cannot tell whether
  prefix-level areas in 04/05/07 map to the register's actual area set (`SR-F-INT`
  vs `SR-I`).
- **Recommended resolution**: One reconciliation pass: (a) correct 03 §22 to state
  which documents actually reference by ID and which are prefix-level pending
  revision; (b) update 04/05/07/12 authoring-time statements to describe the
  delivered package; (c) resolve the `SR-I` → `SR-F-INT` prefix mismatch (see
  RF-012); (d) have 09 confirm every prefix intent was resolved.
- **Disposition**: Must-fix before approval.

### RF-002 — KSPEC-009 misstates the verification evidence status of VT-019 and the overall evidence-status statistics of KSPEC-008

- **Affected**: KSPEC-009 §6 VT evidence table (VT-019 row) and §11 summary;
  contradicts KSPEC-008 §6 (VT-019) and its Evidence Status distribution.
- **Severity**: Critical
- **Evidence**: 08 VT-019 records `Evidence status: Proposed — state-machine
  traversal test not present as a dedicated AS-IS test; partial coverage via
  DeviceServiceActivationTests.cs`. 09's VT table row reads "| VT-019 | SR-F-REG.004
  | AS-IS automated test (state-machine transitions) |", upgrading a Proposed test to
  executed AS-IS coverage. Additionally, 09 §11 claims "7 Proposed, 1 partially
  covered, remainder Covered", while 08 actually contains **12 Proposed / 7
  Partially covered / 39 Covered** (sum 58, verified by count). 09's own table
  contains 11 "Proposed" rows — inconsistent both with 08 and with its own §11.
- **Impact**: The traceability matrix — the package's single downstream consumer of
  VT→SR links per 08 §8 — overstates verification coverage of a lifecycle-behavior
  test and misreports the package's executed/proposed balance, in tension with 08 §8
  rule 2 and the evidence discipline of KSPEC-000 §5 (no upgrade of a class).
- **Recommended resolution**: Recompute 09 §6/§11 from 08's Evidence Status fields
  verbatim; fix the VT-019 row; state the corrected distribution (39 Covered / 7
  Partially covered / 12 Proposed).
- **Disposition**: Must-fix before approval.

### RF-003 — KSPEC-002 §12 status summary does not sum to 38, and the error is propagated into KSPEC-009

- **Affected**: KSPEC-002 §12 (status table); KSPEC-009 §10, §11.
- **Severity**: Major
- **Evidence**: 02 §12 states "Existing 33, Partial 5 (UR-010, UR-012, UR-019,
  UR-036, UR-037), Missing 1 (UR-013)" — 33+5+1 = **39 ≠ 38**. Counting the actual
  status column of the 38 UR body records gives **Existing 32, Partial 5, Missing 1**
  (= 38). 09 §10 quotes "UR statuses quoted from KSPEC-002 (§12: Existing 33,
  Partial 5, Missing 1)" and §11 repeats it, reproducing the error.
- **Impact**: The stakeholder-requirement coverage summary, one of the first tables a
  reviewer checks, is arithmetically wrong; 09's identifier-consistency verification
  (§10) claims to have verified a wrong number.
- **Recommended resolution**: Correct 02 §12 to "Existing 32"; re-derive the
  status counts from the body records; correct 09 §10/§11 quoting.
- **Disposition**: Must-fix before approval.

### RF-004 — Evidence-map claim of "10 test projects" is factually wrong (8 exist)

- **Affected**: KSPEC-000 §10 (evidence source map); KSPEC-008 header (source
  references).
- **Severity**: Major
- **Evidence**: `tests/` contains 8 directories and the solution file contains
  exactly 8 `*.Tests` projects (Domain, Application, Infrastructure, Api, CaHarness,
  Bridge, Kryptonian.MedicalDevice.Enrollment, DimseTlsProxy). 00 §10 says
  "tests/ (10 test projects)" and 08's source references repeat it.
- **Impact**: A trivially checkable factual error in the conventions document and the
  V&V header undermines confidence in the evidence map.
- **Recommended resolution**: Change to 8 in both documents (or state the counting
  convention if a different basis was intended).
- **Disposition**: Must-fix before approval (trivial).

### RF-005 — GAP-007 cites an incorrect EST rate limit ("5 requests/minute")

- **Affected**: KSPEC-010 §7.2 (GAP-007); inconsistent with KSPEC-005 §8 and
  `src/RTSec.Kryptonian.Api/appsettings.json`.
- **Severity**: Major
- **Evidence**: appsettings.json and 05 §8 record `*:/.well-known/est/*` = 100/min
  and 30/min for `simpleenroll`/`simplereenroll`. GAP-007 states "EST endpoints are
  rate-limited (5 requests/minute)".
- **Impact**: A wrong factual number inside a Critical-priority, Wave-0
  deployment-gating finding; the lockout-absence conclusion itself is unaffected and
  well supported by KSPEC-006 THR-009.
- **Recommended resolution**: Correct the parenthetical to the actual configuration
  (100/min general; 30/min per enrollment operation) or remove it; keep the finding's
  substance unchanged.
- **Disposition**: Should-fix (does not alter the Wave-0 conclusion).

### RF-006 — Contradictory statements on activation-code consumption concurrency (TOCTOU closed vs open)

- **Affected**: KSPEC-004 §13 and §16.3 (P-5) vs KSPEC-006 THR-003, KSPEC-008
  VT-048, KSPEC-009 (SR-R.004 row), KSPEC-012 §5.3.
- **Severity**: Major
- **Evidence**: 04 §13 states validation and consumption occur in the enrollment
  path but "no dedicated unique-consumption constraint was observed [INFERRED —
  potential TOCTOU]" and proposes P-5 (unique consumed index or serializable
  transaction). 06 THR-003 states codes are "consumed atomically … concurrent
  activation attempts are serialized (CODE-OBSERVED, `DeviceService` /
  `EnrollmentOrchestrator.TryConsumeActivationCodeAsync`; tests:
  `DeviceActivationConcurrencyTests`)" — and that test file exists in
  `tests/RTSec.Kryptonian.Infrastructure.Tests/Data/`. 08 VT-048 marks the race
  test "Covered — exactly one consumption"; 12 §5.3 requires race-safe consumption
  as TO-BE "per KSPEC-004 §13 TOCTOU finding".
- **Impact**: The package simultaneously claims the race is closed (06/08) and open
  (04/12). One of the two code-level claims is wrong; both 09 SR-R.004 ("Existing")
  and SR-F-REG.002 traceability inherit the ambiguity. This is security-relevant
  (duplicate issuance from one code).
- **Recommended resolution**: Re-inspect the baseline code/test against the actual
  transactional scope; then either (a) correct 04 §13/P-5 (and 12 §5.3) to
  "serialized, regression-covered" or (b) downgrade 06 THR-003's control and 08
  VT-048's evidence status to Proposed and re-rate the residual risk. Record the
  resolution in 09.
- **Disposition**: Must-fix before approval.

### RF-007 — Encoding corruption (mojibake) in the controls baseline KSPEC-000

- **Affected**: KSPEC-000 (00-SPEC-CONVENTIONS.md) — §1 review sequence, §2 header
  controls text; 10 double-encoded UTF-8 sequences found (e.g., "000 â†' 001"
  where "→" is intended; "nameâ€"dateâ€"signature" where "–" is intended).
- **Severity**: Major
- **Evidence**: Character-level scan (U+00E2 / U+20AC / U+00C2 sequences) finds
  corruption only in 00; all other 12 documents are clean UTF-8.
- **Impact**: Cosmetic but located in the binding conventions document, including the
  document-control header fields and the mandatory review sequence — the first
  thing every reviewer reads.
- **Recommended resolution**: Re-save 00 as clean UTF-8 (replace the corrupted
  sequences with "→" and "–"). Recorded here per instructions; not fixed by this
  review.
- **Disposition**: Must-fix before approval (trivial, mechanical).

### RF-008 — KSPEC-014 (package index / README) referenced by the review sequence is missing from the package

- **Affected**: Package completeness; KSPEC-000 §1 and §2 (review sequence ends
  "… 012 → 013 → 014").
- **Severity**: Major
- **Evidence**: `docs/formal-specification/` contains only 00–12;
  `docs/formal-specification/README.md` does not exist (`Test-Path` = False).
- **Impact**: The document hierarchy defines KSPEC-014 as the package index and
  review-sequence entry point; the sequence cannot be executed as documented, and
  approval of the package cannot be declared complete.
- **Recommended resolution**: Author KSPEC-014 (index, review order, status of each
  document) or amend 00 §1 to defer it explicitly.
- **Disposition**: Must-fix before approval.

### RF-009 — SR-F-CLM.005 requires a "configured re-notification interval" that is hard-coded at baseline

- **Affected**: KSPEC-003 §5 (SR-F-CLM.005); corroborated against
  `CertificateExpiryWatcher.cs`.
- **Severity**: Minor
- **Evidence**: The requirement says the gateway "shall re-notify about the same
  certificate no more frequently than the configured re-notification interval", but
  the code holds `private static readonly TimeSpan RenotifyAfter =
  TimeSpan.FromDays(7)` — a constant with no configuration key. The requirement's
  own Evidence field ("24h scan interval, 7-day re-notify") is accurate; the
  requirement wording overstates AS-IS configurability, in tension with the
  package's TBD discipline (KSPEC-000 §8; KSPEC-002 §16.5).
- **Recommended resolution**: Either reword to the fixed 7-day interval at AS-IS
  with a TO-BE note making it configurable, or add the configurability as an
  explicit TO-BE/PROPOSED element.
- **Disposition**: Should-fix.

### RF-010 — KSPEC-005 §9 uses "Conformant (INFERRED)" labels, in tension with the package's no-conformity-claim discipline

- **Affected**: KSPEC-005 §9 (RFC 7030 conformance points table); cf. KSPEC-003
  §16.2 (conformance assessment reserved to KSPEC-011) and KSPEC-011 §1/§8
  vocabulary (Supported / Deviating / Untested).
- **Severity**: Minor
- **Evidence**: Rows such as "`/cacerts` … Conformant (INFERRED)" and "Conformant +
  deviation (lenient)" appear in 05 §9. The section is explicitly disclaimed
  ("shall not be presented as a standards-conformity claim"), but the label wording
  reads as a conformity determination and diverges from the vocabulary every other
  document uses.
- **Recommended resolution**: Replace "Conformant" with 011's vocabulary (e.g.,
  "Aligned (source-inspection)") or "Implemented (inspection)".
- **Disposition**: Should-fix.

### RF-011 — Malformed requirement-area token "SR-C-CLM" in KSPEC-002 traceability notes

- **Affected**: KSPEC-002 (UR-021, UR-025 traceability notes); identifier scheme of
  KSPEC-000 §7.
- **Severity**: Minor
- **Evidence**: The notes read "SR-C-CLM area"; no such area exists — the correct
  areas are `SR-F-CLM` (KSPEC-003 §5) and `SR-C` (KSPEC-003 §12).
- **Recommended resolution**: Replace with "SR-F-CLM / SR-C area".
- **Disposition**: Should-fix.

### RF-012 — `SR-I` prefix defined in KSPEC-000 §7 but never allocated by KSPEC-003

- **Affected**: KSPEC-000 §7 (prefix table lists "SR-I — Interoperability
  requirements — KSPEC-003"); KSPEC-003 §2 (area table has no SR-I; the behavior
  lives in SR-F-INT); KSPEC-007 §2.6/§7.1 (targets SR-I-*); KSPEC-009 §9.4 item 6.
- **Severity**: Minor
- **Evidence**: The conventions document binds an area the canonical register does
  not define; 07 still references it. 09 flags the mismatch for 07 but cannot amend
  00 or 03.
- **Recommended resolution**: Remove `SR-I` from 00 §7 (or have 03 allocate it) and
  re-point 07 §7.1/§2.6 at `SR-F-INT` (09 §9.4 item 6 already recommends this).
- **Disposition**: Should-fix.

### RF-013 — KSPEC-011 §11.2 (ASVS V13) overstates OpenAPI file currency

- **Affected**: KSPEC-011 §11.2 (V13 row); inconsistent with KSPEC-005 §11 and
  KSPEC-003 SR-F-INT.003/004.
- **Severity**: Minor
- **Evidence**: 11 states "OpenAPI files (Admin/StatusObserve current)" while 05 §11
  records `OpenAPI.Admin.yaml`/`OpenAPI.ESTProfiles.yaml` as contract-first designs
  **without security-scheme declarations and missing implemented paths**,
  `OpenAPI.Schema.yaml` stale, `OpenAPI.Client.yaml` empty (0 bytes), and EST paths
  absent from all OpenAPI files — gaps 11 itself cites elsewhere (GAP-029, V13 gaps
  column).
- **Recommended resolution**: Reword the V13 coverage cell to "partial /
  contract-first; Client empty, Schema stale (GAP-029)".
- **Disposition**: Should-fix.

---

## 3. Overall review verdict

**Package verdict: technically sound, evidence-faithful Draft — NOT ready for
approval in its current form.** Approval is blocked pending the one Critical and
seven Major findings (RF-001 – RF-008); the five Minor findings (RF-009 – RF-013)
should be corrected in the same revision pass.

The package demonstrates strong engineering discipline and outstanding evidence
fidelity: every requirement record carries classification, status, provenance,
verification method, and concrete repository evidence; hard-coded threshold values
are consistently handled as TBD; no approval is inferred from implementation; the
no-conformity / no-medical-device-regulatory boundary is maintained in all 13
documents; and the repository spot-checks (test files, artifacts, controller
inventory, configuration constants) confirmed every sampled citation. The principal
weakness is the parallel-authoring seam: stale authoring-time statements, the
`SR-I` / `SR-F-INT` prefix split, the VT-019 evidence upgrade, and two summary-count
errors. None of these undermine the technical substance, and all are mechanically
fixable in a single reconciliation pass. Re-review after the must-fix dispositions
should suffice; no re-authoring of any document body is required.

Finding counts: **1 Critical (RF-002), 7 Major (RF-001, RF-003 – RF-008), 5 Minor
(RF-009 – RF-013); 8 must-fix, 5 should-fix dispositions** (RF-005 is Major with a
should-fix disposition).

## 4. Per-document readiness summary

| Document | Readiness | Notes |
|---|---|---|
| KSPEC-000 Conventions | Revise (minor) | RF-007 (mojibake), RF-004 (test-project count), RF-012 (SR-I prefix) |
| KSPEC-001 Product definition | Ready | UC/SCN/ASM/EXC element sets complete and consistently consumed by 02; TBD-001–006 well formed |
| KSPEC-002 User requirements | Near-ready | RF-003 (§12 count), RF-011 (area token); body records and §15 evidence basis otherwise consistent |
| KSPEC-003 Software requirements | Ready for review | Register internally consistent (154 = 154, no duplicates); RF-006 (SR-R.004 vs 04), RF-009 (SR-F-CLM.005 wording), RF-012 |
| KSPEC-004 Software design | Ready pending reconciliation | RF-001 (§1.3/§18 stale), RF-006 (§13/P-5 vs 06/08); architecture, state machines, and design-to-register references otherwise verified |
| KSPEC-005 Interface control | Ready pending reconciliation | RF-001 (preamble/§19), RF-010 ("Conformant" labels); endpoint tables match the controller inventory |
| KSPEC-006 Cybersecurity | Ready | THR count verified (18); all cited test files exist; bands consistent; RF-006 is the only cross-document tension |
| KSPEC-007 Operational risk | Ready pending reconciliation | RF-001 (§2.6), RF-012 (SR-I); FMEA arithmetic recomputed — correct; 16 ORK entries match header |
| KSPEC-008 V&V | Near-ready | RF-004 (header "10 test projects"); 58 VT records verified present and well-formed; evidence discipline exemplary |
| KSPEC-009 Traceability | Needs revision | RF-002 (VT-019 row + §11 statistics), RF-003 (quoting 02's wrong count); matrices otherwise matched the 03/08 registers on all spot-checks |
| KSPEC-010 Gap analysis | Near-ready | RF-005 (GAP-007 rate value); 37 GAP entries verified; Wave ordering internally consistent |
| KSPEC-011 Standards conformance | Ready | Discipline exemplary; RF-013 (V13 wording) only |
| KSPEC-012 Engineering handoff | Ready pending reconciliation | RF-001 (header/§9/§13.2 staleness); concrete SR references verified against the register |
| KSPEC-013 (this document) | Draft | Subject to its own approval cycle |
| KSPEC-014 README | **Missing** | RF-008 |

---

*End of KSPEC-013, version 0.1 (Draft). This review records findings only; no
specification file other than this document was created or modified, and no approval
is recorded or inferred.*

## Post-review resolution log (2026-10-13, package integration pass)

The following findings were dispositioned after the independent review. Each disposition
was executed by the package integration team (not by the original document authors) and
was verified against repository evidence before application.

| RF | Severity | Disposition | Resolution |
|---|---|---|---|
| RF-001 | Major | Accepted with reconciliation | Prefix-level SR references in KSPEC-004, 005, 007, 012 are now explicitly resolved via KSPEC-009 §7 binding tables; package index (KSPEC-014 §5) records this rule. No document-body re-authoring performed. |
| RF-002 | Critical | Fixed | KSPEC-009 §6 VT-019 evidence status corrected to Proposed (matching KSPEC-008); §11 VT distribution corrected to 12 Proposed / 7 partially covered / 39 Covered. |
| RF-003 | Major | Fixed | KSPEC-002 §12 status summary corrected to Existing 32 / Partial 5 / Missing 1 (sums to 38); KSPEC-009 §10 quote updated accordingly. |
| RF-005 | Major | Fixed | KSPEC-010 GAP-007 rate-limit figures corrected to the baseline values (100/min general; 30/min per EST enroll endpoint, per docs/ARCHITECTURE.md). |
| RF-004 | Major | Fixed | Test-project count corrected from 10 to 8 across KSPEC-000 §10, KSPEC-003, KSPEC-008, KSPEC-009, KSPEC-010 (tests/ contains 8 test projects at baseline). |
| RF-006 | Major | Resolved in favor of race-safe claim | Adjudicated against source: `UnitOfWork.TryConsumeActivationCodeAsync` enforces one-time consumption under optimistic concurrency (`DbUpdateConcurrencyException` → rejection) and `DeviceActivationConcurrencyTests` proves two contexts cannot both consume. KSPEC-004 §13/P-5 and KSPEC-012 §5.3 corrected; the genuinely open item is the crash-scope of consumption vs issuance (KSPEC-004 §17 item 1). |
| RF-007 | Major | Fixed | Encoding corruption in KSPEC-000 repaired; file verified clean UTF-8. |
| RF-008 | Major | Fixed | KSPEC-014 package index (README.md) created per KSPEC-000 §1. |

Remaining findings (should-fix / note) stay open against this Draft package and must be
dispositioned during formal review before any document is promoted to Approved.

| RF | Severity | Disposition | Notes |
|---|---|---|---|
| RF-009 | Minor | Open | Kept for formal review; see finding detail above. |
| RF-010 | Minor | Open | Kept for formal review; see finding detail above. |
| RF-011 | Minor | Open | Kept for formal review; see finding detail above. |
| RF-012 | Minor | Open | Kept for formal review; see finding detail above. |
| RF-013 | Minor | Open | Kept for formal review; see finding detail above. |

Note: resolutions applied are documentation corrections only; no application source code
was modified at any point (KSPEC-000 §13).

## Current disposition after reconciliation (0.2, 2026-10-13)

The initial findings and verdict above are a historical review of version 0.1. They
are not the current package status. The earlier integration log accidentally swapped
RF-004 (test-project count) and RF-005 (rate limits); those rows now match the original
finding identifiers. The original summary table remains historical evidence.

| Finding | Current disposition | Resolution |
|---|---|---|
| RF-001 | Closed - corrected | Stale sibling-document text removed; canonical register and concrete binding matrix referenced. |
| RF-002 through RF-008 | Closed - documentation corrections | Prior corrections retained; encoding rechecked and residual KSPEC-000 mojibake actually repaired in 0.2. |
| RF-009 | Closed - corrected | SR-F-CLM.005 Partial; fixed AS-IS timing and TO-BE configurable interval separated. |
| RF-010 | Closed - corrected | Implemented/source-inspection vocabulary replaces Conformant labels. |
| RF-011 | Closed - corrected | Malformed SR-C-CLM area references replaced by allocated SR-F-CLM / SR-C areas. |
| RF-012 | Closed - corrected | Unallocated SR-I prefix removed from current conventions/risk/interface references; interoperability is SR-F-INT. |
| RF-013 | Closed - corrected | ASVS API row describes partial contract-first coverage and known drift. |
| RF-014 through RF-024 | Closed - specification corrections | Detailed source observations/dispositions in KSPEC-016; underlying code gaps remain open. |

The package remains Draft with stakeholder decisions and implementation gaps open.
Closing a documentation finding does not approve the product or satisfy an unexecuted test.
