# Sequence Diagrams

This folder contains Mermaid sequence diagrams for the main Kryptonian medical-device certificate lifecycle flows.

The diagrams are source-controlled as `.mmd` files so they can be rendered by GitHub, Mermaid CLI, VS Code Mermaid preview extensions, or documentation tooling.

## Diagram Index

| Diagram | Purpose |
| --- | --- |
| [01-admin-device-registration-and-activation-code.mmd](01-admin-device-registration-and-activation-code.mmd) | Administrator creates a pending device record and generates the one-time bootstrap activation code. |
| [02-medical-device-enrollment.mmd](02-medical-device-enrollment.mmd) | Medical device first enrollment through EST `simpleenroll` with activation-code validation and active CA backend issuance. |
| [03-medical-device-re-enrollment.mmd](03-medical-device-re-enrollment.mmd) | Medical device renewal through EST `simplereenroll` using the existing device certificate for mTLS authentication. |
| [04-medical-device-certificate-revocation-and-trust-removal.mmd](04-medical-device-certificate-revocation-and-trust-removal.mmd) | Implemented trust-removal flow and the connector-level CA revocation hook. |
| [05-est-cacerts-bootstrap-trust.mmd](05-est-cacerts-bootstrap-trust.mmd) | Device retrieves the gateway CA chain from EST `cacerts` before validating issued certificates. |
| [06-enrollment-rejection-notification.mmd](06-enrollment-rejection-notification.mmd) | Rejected enrollment or re-enrollment audit and email notification flow. |
| [07-certificate-expiry-and-renewal-monitoring.mmd](07-certificate-expiry-and-renewal-monitoring.mmd) | Background expiry watcher notification flow for certificates near expiry without a newer renewal. |
| [08-dicom-mtls-transfer-with-gateway-issued-certs.mmd](08-dicom-mtls-transfer-with-gateway-issued-certs.mmd) | DICOM TLS demo flow after two devices receive gateway-issued certificates. |

## Implementation Notes

- First enrollment is activation-code based. The device calls `/.well-known/est/simpleenroll` with a PKCS#10 CSR and activation metadata headers.
- Re-enrollment always requires a client certificate and calls `/.well-known/est/simplereenroll`.
- Device-facing enrollment uses the active CA backend selected by the gateway, even though EST profiles still carry legacy CA backend metadata.
- Direct certificate revocation is represented in the domain connector contract as `ICaConnector.RevokeCertificateAsync`, and the EJBCA harness connector implements a revoke call. The current admin API does not expose a gateway endpoint that calls it.
- The implemented user-facing trust removal action is `POST /api/devices/{id}/remove`; after that, future renewals are rejected because the device is no longer active.
