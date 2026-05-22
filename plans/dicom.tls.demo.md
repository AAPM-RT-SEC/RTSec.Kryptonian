# Kryptonian DICOM TLS Demo

## Summary

Build a new console project, `Kryptonian.DICOMTls`, that runs two logical Kryptonian medical-device instances in one process. Each instance enrolls through the hosted Kryptonian Gateway using its own activation token, receives its own certificate, starts/uses fo-dicom DIMSE TLS with mutual certificate authentication, and transfers generated dummy DICOM files between the devices.

Because the app receives two activation tokens from the caller, it does not create devices or activation codes through the admin API.

## Key Changes

- Add `src/Kryptonian.DICOMTls/Kryptonian.DICOMTls.csproj` and include it in `RTSec.Kryptonian.sln`.
- Refactor reusable EST enrollment logic out of `Kryptonian.MedicalDevice` UI code into public non-UI classes usable by both the WPF app and the new demo.
- `Kryptonian.DICOMTls` accepts gateway, two activation codes, CN/serial/AE/port, output count, and artifact path settings.
- Device A and Device B each generate a fresh RSA key pair and CSR, call EST `/.well-known/est/simpleenroll`, extract the leaf certificate and CA chain from the PKCS#7 response, and keep an exportable certificate with private key in memory.
- Use fo-dicom `5.2.6` for generated DICOM files, Store SCP, Store SCU, DIMSE association, C-STORE, and TLS transport.
- Validate both peers by building certificate chains against the gateway-returned CA chain.

## Implementation Notes

- The demo models two instances as independent `MedicalDeviceNode` objects in one process, each with separate identity, certificate, AE title, and transfer state.
- Default identities:
  - Device A CN: `kryptonian-dicom-scu-{timestamp}`
  - Device B CN: `kryptonian-dicom-scp-{timestamp}`
  - AE titles: `KRYPTSCU` and `KRYPTSCP`
  - Device B listen port: `11114`
- Generated DICOM files are minimal valid Secondary Capture objects with unique Patient/Study/Series/SOP UIDs and dummy pixel data.
- The receiver stores received files in `artifacts/dicom-tls-demo/received/` and keeps an in-memory list for assertion.
- The sender fails the run if any C-STORE response is not success or if the receiver count/SOP UID set does not match.
- The demo does not depend on Windows certificate store installation.

## Test Plan

- Build with `dotnet build RTSec.Kryptonian.sln`.
- Happy-path manual run with two valid activation tokens confirms both certificates are issued by the gateway-backed CA, Device A sends generated files over TLS, and Device B receives exactly the expected SOP Instance UIDs.
- Negative scenarios: missing token exits with usage, invalid/used token reports EST failure, and peer trust validation failure rejects DIMSE TLS.

## Assumptions

- The caller supplies two fresh, unused activation tokens, one per demo device.
- The hosted gateway URL remains `https://kryptonian-gateway.mangotree-b3d09362.eastus.azurecontainerapps.io`.
- The active gateway CA backend returns enough CA chain material in the EST PKCS#7 response for local trust validation.
- Azure CLI inspection is only needed if enrollment fails against the hosted gateway during verification.
