# Finalize Kryptonian Gateway

## Summary

Build Kryptonian into the .NET flagship competitive gateway backend with a portable JavaScript/TypeScript operator UI. Scope is limited to the contestant-side gateway API, TypeScript UI, CA connectors, and contestant-side DICOM bridge. Do not modify `RTSec.Kryptonian.CaHarness` except for read-only reference while matching documented protocols.

The gateway is CA-backend agnostic from the medical-device perspective: devices enroll through the same EST surface at `/.well-known/est`, while administrators configure backend-specific CA settings and choose which CA backend is active.

## Application Stack

- Keep the gateway implementation .NET-first where it matters for the contest:
  - ASP.NET Core hosts the EST device surface, REST APIs, connector orchestration, persistence, and demo endpoints.
  - The DICOM bridge remains a .NET worker/service using fo-dicom.
  - Shared business rules live behind REST APIs and application services, not inside the UI.

- Replace the Blazor operator UI with a JavaScript/TypeScript frontend:
  - Do not build new contestant-facing UI in Blazor.
  - Add a standalone TypeScript app, preferably Vite + React unless the repo establishes a different TypeScript standard before implementation.
  - The TypeScript app consumes the gateway REST APIs and can be served by ASP.NET Core static file hosting or run independently during development.
  - Keep the API contract generic enough that other teams can reuse the same backend from non-.NET UIs.

- Treat the existing `RTSec.Kryptonian.Web` Blazor app as transitional:
  - Do not expand it for the final showcase UI.
  - Either leave it dormant during the migration or remove it after the TypeScript UI covers the same workflows.
  - Avoid duplicating business logic between Blazor and TypeScript during the transition.

## Core Gateway Contracts

- Bind device identity to enrollment with a CSR subject contract:
  - Each registered device stores an expected subject common name as `Device.SubjectCommonName`.
  - Demo-generated CSRs use `CN={SubjectCommonName}`.
  - Incoming EST `simpleenroll` requests parse the PKCS#10 subject CN and require exactly one active device with that CN.
  - Unknown, pending, or removed devices are rejected before any CA connector is called, and an `EnrollmentEvent` is recorded with `Rejected` status and the reason.

- Make active-backend selection explicit and single-sourced:
  - Add `CaBackend.IsActive` with a uniqueness guard so only one backend is active at a time.
  - Keep `EstProfile.CaBackendId` for legacy profile metadata, but enrollment routing must override it with the currently active `CaBackend`.
  - The default device-facing EST profile remains stable; switching the active backend changes future enrollments only.
  - The active backend read model is exposed on CA backend list results and through `GET /api/cas/active`.

- Preserve issued certificate history across backend switches:
  - `GET /api/devices/{id}/certificates` returns all certificates issued for the device, grouped by issuing backend and ordered newest first.
  - Previously issued certificates remain usable for harness scoring after their backend is no longer active.
  - The UI shows per-backend "last successful enrollment" and observed gateway OID status so judges can see all four Flow 1 backend checks at once.

- Store demo keys and certificates for repeatable flows:
  - Demo-generated private keys are stored encrypted with ASP.NET Core Data Protection in the gateway database.
  - Stored certificate rows include PEM, DER base64, serial number, issuing backend id/type, subject CN, validity, and observed gateway OID.
  - Production documentation must note that real medical devices should own their private keys.

## Enrollment And Backend Implementation

- Keep the device-facing enrollment protocol backend-agnostic:
  - Devices do not pass backend ids, backend names, or CA-specific settings.
  - Do not add `/api/devices/{id}/enroll/{backend}`.
  - Admin/demo enrollment uses `POST /api/devices/{id}/demo-enroll`, which generates a keypair/CSR for the device and enrolls through the active backend.

- Complete active CA backend administration:
  - Keep existing `/api/cas` CRUD/test behavior.
  - Add `POST /api/cas/{id}/activate`.
  - Add `GET /api/cas/active`.
  - Activation updates all affected backends transactionally so near-simultaneous enrollments cannot partially route.

- Assert gateway-path issuance:
  - After every successful enrollment, parse the returned certificate and record any expected harness OID.
  - Validate the per-backend OIDs used for gateway scoring: `1.3.6.1.4.1.99999.1`, `1.3.6.1.4.1.99999.2`, and `1.3.6.1.4.1.99999.3`.
  - Surface missing or unexpected OIDs in the UI before the operator attempts harness scoring.

- Wire ACME as a first-class upstream CA connector:
  - Keep the validated ACME HTTP-01 challenge relay behavior.
  - After successful ACME enrollment, the demo flow posts the issued certificate PEM to `/teams/{token}/api/backends/acme/claim`.
  - The UI records and displays the ACME claim response because the DIMSE TLS proxy cannot score ACME certificates.

- Add gateway-wide hackathon settings:
  - Store `HarnessBaseUrl`, `TeamToken`, DIMSE host/ports, DICOMweb base URL, called AE title, bridge AE title, and bridge listen port in configuration with UI editing.
  - The team token is gateway-wide because enrollment demos, ACME claim, Flow 2 STOW, Flow 3 claim, and scoreboard reads all need the same token.

## DICOM Bridge Implementation

- Add a contestant-side .NET bridge worker/service:
  - Use fo-dicom for DIMSE C-MOVE, C-STORE SCP, and C-STORE SCU behavior.
  - Use a MIME-capable parser for DICOMweb multipart WADO responses; do not hand-roll multipart boundary parsing.
  - Keep the bridge separate from `RTSec.Kryptonian.DimseTlsProxy`.

- Clarify `RTSec.Kryptonian.DimseTlsProxy` disposition:
  - Leave the existing project dormant and unchanged for this effort.
  - Treat it as harness-side/reference infrastructure, not as the contestant bridge.
  - The new bridge connects to deployed Orthanc/proxy endpoints but does not repurpose the existing TLS proxy project.

- Implement Flow 1 demo support:
  - For selfsigned, ADCS, and EJBCA certificates, use the stored certificate/private key to perform mTLS C-STORE to `kryptonian-dimse.eastus.cloudapp.azure.com:4243`.
  - Bootstrap trust for the DIMSE TLS proxy by downloading or configuring the PEM from `http://kryptonian-dimse.eastus.cloudapp.azure.com:8044/server-cert`.
  - Store the trusted proxy certificate thumbprint and log its SHA-256 during startup/demo setup.

- Implement Flow 2 DIMSE-to-DICOMweb:
  - Start a local non-TLS C-STORE SCP with configurable AE title and port.
  - Send C-MOVE to Orthanc DIMSE using the configured called AE title and destination AE title.
  - On received C-STORE, forward the DICOM object to the harness STOW endpoint.
  - Include `X-Device-Certificate: <certificateDerBase64>` from the most recent non-ACME enrolled certificate selected for the demo.
  - Include `X-Transfer-Mode: cmove`.
  - Persist job status, SOP Instance UID, selected certificate serial number, HTTP status, and response body.

- Implement Flow 3 DICOMweb-to-DIMSE:
  - Retrieve the selected DICOM object through DICOMweb WADO-RS.
  - Extract the DICOM payload from multipart or single-part responses.
  - Send the object to Orthanc over non-TLS DIMSE C-STORE.
  - Claim completion with `POST /teams/{token}/scoring/dicomweb-to-dimse`.
  - Treat a 200 response from the harness claim endpoint as authoritative and persist the final scoreboard response.

## TypeScript UI And Demo Flow

- Add operational pages in the TypeScript frontend for:
  - Hackathon settings and connection checks.
  - Device registration, approval, removal, certificate history, and rejection events.
  - CA backend configuration, testing, and active-backend selection.
  - Active-backend demo enrollment.
  - Flow 1 per-backend progress.
  - Flow 2 and Flow 3 bridge jobs.
  - Final harness scoreboard snapshot.

- Make the single-active-backend scoring workflow explicit:
  - The intended Flow 1 sweep is activate selfsigned -> enroll -> score, activate ADCS -> enroll -> score, activate EJBCA -> enroll -> score, activate ACME -> enroll -> claim.
  - The UI keeps all four per-backend results visible after switching active backend.
  - Existing certificates remain available for Flow 1 evidence and bridge selection.

- Keep the frontend backend-agnostic and reusable:
  - Generate or maintain a typed API client from the gateway OpenAPI document when practical.
  - Store no CA-specific routing decisions in frontend state beyond the administrator-selected active backend.
  - Make all demo buttons call gateway-owned APIs; the browser should not directly speak EST, SCEP, ACME, DIMSE, or DICOMweb.
  - Support `.env` configuration for the gateway API base URL so the same UI can run against local or deployed gateway instances.

## Public Interfaces

- Device APIs:
  - `GET /api/devices`
  - `POST /api/devices`
  - `POST /api/devices/{id}/approve`
  - `POST /api/devices/{id}/remove`
  - `GET /api/devices/{id}/certificates`
  - `POST /api/devices/{id}/demo-enroll`

- CA backend admin APIs:
  - `GET /api/cas`
  - `POST /api/cas`
  - `PUT /api/cas/{id}`
  - `POST /api/cas/{id}/test`
  - `POST /api/cas/{id}/activate`
  - `GET /api/cas/active`

- Gateway demo APIs:
  - Add gateway-owned endpoints for live validation flows against the existing harness.
  - These endpoints call harness URLs but do not change harness code.
  - Demo enrollment returns issued cert PEM, DER base64, serial number, observed OID, issuing backend, and any harness scoring or claim response.

- Bridge-to-gateway contract:
  - The bridge fetches certificate material through an internal gateway API or application service.
  - Flow 1 mTLS uses certificate plus encrypted private key.
  - Flow 2 STOW uses the certificate DER base64 for `X-Device-Certificate`.
  - ACME certificates are excluded from mTLS proxy scoring and use the ACME claim endpoint instead.

## Test Plan

- Unit tests:
  - Device state transitions.
  - CSR subject CN binds to the correct active device.
  - Enrollment policy blocks unknown, pending, and removed devices before connector dispatch.
  - Rejected enrollments produce `EnrollmentEvent` records with status and reason.
  - Enrollment routing uses only the active CA backend, ignoring `EstProfile.CaBackendId` for connector selection.
  - Backend activation enforces one active backend and handles toggle races transactionally.
  - Stored certificates record expected gateway OIDs.
  - ACME connector performs challenge relay and the demo flow performs the ACME claim POST.

- Integration tests:
  - Approved device enrolls through the active backend.
  - Changing the active backend changes future enrollments only.
  - Previously issued certificates remain queryable after backend switches.
  - Removed device cannot re-enroll.
  - DICOMweb WADO parser handles multipart boundaries, missing content type, split chunks, and single-part DICOM responses.
  - Bridge can send and receive DIMSE objects on loopback using fo-dicom.
  - Flow 2 STOW includes `X-Device-Certificate` and `X-Transfer-Mode: cmove`.
  - TypeScript UI can activate a backend, register and approve a device, run demo enrollment, and display stored certificate history against the local gateway API.
  - TypeScript UI keeps per-backend Flow 1 status visible after active-backend switches.

- Live smoke test:
  - Register a fresh harness team.
  - Configure Kryptonian with the harness URL and team token.
  - Activate each CA backend one at a time and run the same EST device enrollment path.
  - Assert the expected gateway OID is present before attempting each score.
  - Run Flow 1 for selfsigned, ADCS, EJBCA, and ACME, using ACME claim for ACME.
  - Run Flow 2 and Flow 3 against the existing deployed harness.
  - Save the team token and final scoreboard JSON snapshot as a build artifact.
  - Confirm the external harness scoreboard without modifying harness code.

## Risk Register

| Risk | Likelihood | Impact | Mitigation |
|---|---:|---:|---|
| Device-to-CSR binding ambiguity blocks enrollment policy | Medium | High | Use exact CSR subject CN to `Device.SubjectCommonName` binding for v1. |
| Active-backend UX confuses judges | Medium | Medium | Keep per-backend result indicators visible after switches and provide a scripted flow. |
| OID validation gap causes silent scoring loss | Medium | High | Parse and display expected OIDs before scoring. |
| DIMSE TLS proxy trust misconfiguration | Medium | High | Bootstrap or configure proxy PEM, persist thumbprint, and log SHA-256. |
| DICOMweb multipart parser edge cases | Medium | Medium | Use a MIME parser and cover malformed/split responses in tests. |
| ACME claim step is missed | Medium | Medium | Make claim POST part of the ACME demo action, not an optional manual step. |

## Assumptions

- `RTSec.Kryptonian.CaHarness` is read-only for this effort.
- The deployed harness is already validated and acts as the external acceptance system.
- The work is limited to the gateway API, TypeScript operator UI, gateway connectors, and contestant-side DICOM bridge.
- The .NET flagship may use fo-dicom for DIMSE behavior.
- Demo key generation/storage is acceptable for the showcase app; production guidance should still note that real devices should own private keys.
