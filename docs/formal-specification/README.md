# Kryptonian Formal Engineering Specification — Package Index

| Field | Value |
|---|---|
| Document ID | KSPEC-014 |
| Title | Formal Specification Package Index and Review Sequence |
| Version | 0.2 (Draft) |
| Status | Draft — pending review |
| Created | 2026-10-13 |
| Baseline commit | `682f996c8447afec13bc1fce5b7f59e6a06efa25` (branch `feat/windows-sandbox-installer`) |
| Repository | https://github.com/AAPM-RT-SEC/RTSec.Kryptonian |
| Revision history | 0.1 initial draft; 0.2 code/document reconciliation, 2026-10-13 (KSPEC-016) |
| Source references | KSPEC-000 evidence map; KSPEC-016 reconciliation evidence |
| Author | Unassigned - no approval recorded |
| Reviewer | Unassigned - no approval recorded |
| Approver | Unassigned - no approval recorded |

## 1. Purpose of this package

This directory contains the formal engineering specification for **RTSec.Kryptonian**,
a healthcare IT cybersecurity infrastructure system: an EST (RFC 7030) certificate
enrollment, identity provisioning, and credential lifecycle management gateway that
connects network devices — including medical devices — to institution-managed
certificate authorities.

The package was produced by reverse-engineering the repository at the baseline commit
above and transforming the observed prototype into an implementation-independent,
verifiable specification. It is intended to be handed to an independent engineering
team for implementation, testing, maintenance, and future development.

**Classification (binding):** Kryptonian is enterprise IT/security infrastructure.
It performs no clinical functions and is not a regulated medical device. Device
identity, certificate enrollment authorization, network trust, application-level
authorization, and clinical capability remain distinct throughout; no clinical
capability may be derived from Kryptonian state.

## 2. Document hierarchy

| Document ID | File | Role | Primary audience |
|---|---|---|---|
| KSPEC-000 | `00-SPEC-CONVENTIONS.md` | Conventions, terminology, provenance and identifier controls | All readers; read first |
| KSPEC-001 | `01-PRODUCT-DEFINITION.md` | Purpose, stakeholders, boundaries, use cases, scope | Everyone |
| KSPEC-002 | `02-USER-REQUIREMENTS.md` | Stakeholder and user requirements (UR-001 – UR-038) | Product owner, stakeholders |
| KSPEC-003 | `03-SOFTWARE-REQUIREMENTS.md` | **Normative software requirements specification** (154 SR-* requirements + canonical register) | Engineering, QA |
| KSPEC-004 | `04-SOFTWARE-DESIGN.md` | Architecture and design (AS-IS and TO-BE, state machines, data model) | Engineering |
| KSPEC-005 | `05-INTERFACE-CONTROL.md` | EST and admin API interface contracts, CA connector contract | Engineering, integrators |
| KSPEC-006 | `06-CYBERSECURITY.md` | STRIDE threat model, controls, residual risk (THR-001 – THR-018) | Security, engineering |
| KSPEC-007 | `07-OPERATIONAL-RISK.md` | FMEA operational risk assessment and recovery design (ORK-001 – ORK-016) | Operations, engineering |
| KSPEC-008 | `08-VERIFICATION-VALIDATION.md` | V&V strategy and test specification (VT-001 – VT-060) | QA, engineering |
| KSPEC-009 | `09-TRACEABILITY.md` | Bidirectional traceability UR → SR → architecture → evidence → VT | QA, review board |
| KSPEC-010 | `10-GAP-ANALYSIS.md` | 40 findings (GAP-001 – GAP-040) between baseline and target | Engineering, product owner |
| KSPEC-011 | `11-STANDARDS-CONFORMANCE.md` | Standards readiness analysis; no conformity claims | Security, standards liaison |
| KSPEC-012 | `12-ENGINEERING-HANDOFF.md` | Implementation-independent handoff, roadmap, definition of done | Incoming engineering team |
| KSPEC-015 | `FINAL-SUMMARY.md` | Package synthesis and approval limits | All readers |
| KSPEC-016 | `13-CODE-DOCUMENT-RECONCILIATION.md` | Current source audit, corrections and evidence limits | Engineering, reviewers |
| KSPEC-013 | `REVIEW-FINDINGS.md` | Independent review findings (RF-001 – RF-024; original review and current reconciliation) and resolution log | Review board |

## 3. Review sequence

Read in this order:

1. **KSPEC-000** — conventions (mandatory baseline for identifiers and provenance).
2. **KSPEC-001** → **KSPEC-002** → **KSPEC-003** — what the product is, who needs it,
   and the normative requirements.
3. **KSPEC-004**, **KSPEC-005**, **KSPEC-006**, **KSPEC-007** — how it is designed,
   what interfaces exist, what the security model is, and what can fail.
4. **KSPEC-008** and **KSPEC-009** — how conformance is verified and traced.
5. **KSPEC-010**, **KSPEC-011** — what is missing and how the implementation compares
   with applicable standards.
6. **KSPEC-012** — the handoff synthesis for the incoming team.
7. **KSPEC-013** — independent review status and open findings.

## 4. Review and approval workflow

1. Every document carries a doc-control header (KSPEC-000 §2) with approval
   placeholders (Author / Reviewer / Approver). All documents are **Draft**;
   approval is never inferred from implementation.
2. Requirements carry Approval = Draft or Review Required. Approval transitions are a
   stakeholder decision recorded in the document revision histories.
3. Unresolved thresholds and design decisions are recorded as **TBD** and collected in
   the decisions register of KSPEC-012 §10 (DEC-001 – DEC-020); they must never be
   resolved by guessing values.
4. Independent review findings (KSPEC-013) must be dispositioned before any document
   is promoted to Approved.

## 5. Identifier discipline

Requirement, test, threat, risk, gap, and finding identifiers are allocated per
KSPEC-000 §7 and are stable and never reused. The canonical register of all SR-*
requirements is KSPEC-003 (Requirements Register section); traceability binding is
KSPEC-009. Prefix-level SR references remaining in KSPEC-004, KSPEC-005, KSPEC-007,
and KSPEC-012 (a parallel-authoring artifact, finding RF-001) are resolved by the
KSPEC-009 §7 binding tables and do not allocate new identifiers.

## 6. Evidence discipline

- Baseline: commit `682f996c8447afec13bc1fce5b7f59e6a06efa25`.
- Provenance classes (VERIFIED / CODE-OBSERVED / DOCUMENTED / INFERRED / PROPOSED /
  UNKNOWN) are used per KSPEC-000 §5; executed-evidence records live under
  `artifacts/developer/verify-*/` and `docs/IMPLEMENTATION-TRACKER.md`.
- No document asserts standards conformity, third-party certification, approvals, or
  medical-device regulatory status. The prototype is reference material only;
  where it conflicts with this specification, the specification governs.

## 7. Revision history

| Version | Date | Change |
|---|---|---|
| 0.1 | 2026-10-13 | Initial package: documents KSPEC-000 – KSPEC-013 created; independent review (KSPEC-013) performed; RF-002, RF-003, RF-004, RF-005, RF-006, RF-007 corrections applied (see KSPEC-013 resolution log). |
| 0.2 | 2026-10-13 | Reconciled source behavior and evidence; corrected requirement statuses and traceability; added KSPEC-016, GAP-038–040 and VT-059–060; added reproducible PDF publication and fresh regression records. |


## 8. Editable source and production PDF

The `.md` files are the authoritative specification source. The publication is
`Kryptonian-Formal-Specification.pdf` in this directory. It contains the complete
17-document package, rendered Mermaid diagrams, document bookmarks, a contents page,
page numbers and explicit Draft review status. Diagram sheets use larger page sizes
to retain legible labels. Wide Markdown tables publish as labeled
records so their text stays readable and no columns are silently dropped.

Install the pinned build dependencies once from the repository root:

```powershell
python -m pip install -r docs/formal-specification/tools/requirements.txt
npm ci --prefix docs/formal-specification/tools
```

Validate and rebuild after editing the Markdown:

```powershell
python docs/formal-specification/tools/validate_specs.py
python docs/formal-specification/tools/build_pdf.py
```

`validation/verification.json` reports mechanical checks and available fresh test
evidence. `validation/publication-manifest.json` records ordered source hashes,
dependency versions, output hash, page count and diagram count. Generated diagram
images, page previews, logs and test results are ignored; Markdown, build tools,
dependency lockfiles, manifests and the final PDF are retained.

Reproduce application checks separately:

```powershell
dotnet test RTSec.Kryptonian.sln --logger trx --results-directory docs/formal-specification/validation/test-results
```

A link to a VT means a specified verification obligation, not that its full procedure
has passed. Source inspection, existing test definitions, historical live evidence,
and freshly executed tests retain distinct provenance. See KSPEC-016 first for the
current reconciliation findings. This PDF is production-quality documentation of a
Draft specification; it does not assert production readiness of the application.
