# Background: Why This Hackathon Matters

## The Problem

Medical device manufacturers do not control the certificate authority infrastructure inside hospitals.

One hospital may use Microsoft ADCS. Another may use Sectigo, DigiCert, Entrust, EJBCA, Vault, ACME, a self-signed private CA, or a custom internal PKI. The manufacturer still has to make the device work in all of those environments.

That creates a difficult interoperability problem:

> The medical device manufacturer needs one certificate-enrollment workflow that can work across many different hospital certificate authority choices.

Ideally, a medical device would implement one standard protocol, such as EST, and the hospital would deploy an intermediary service that maps that device-side protocol to whatever CA infrastructure the hospital already uses.

That intermediary is generically called a **Certificate Enrollment Gateway**.

## Desired Pattern

The desired architecture is:

```text
Medical Device
    -> standard enrollment protocol, such as EST
    -> Certificate Enrollment Gateway
    -> hospital-selected CA backend
```

The device should not need custom code for every hospital PKI.

The hospital should not need to replace its CA just because a device vendor only supports one certificate system.

The gateway should provide:

- Device activation before certificate issuance
- Certificate enrollment and renewal
- Mapping to different CA backends
- Audit trails
- Administrative trust removal
- A clear path for securing DICOM transport

## Existing Solutions

Several existing products and open-source systems solve important parts of this problem. The hackathon should acknowledge them directly.

### Picocrypt Certificate Enrolment Gateway

Picocrypt offers a product called **Certificate Enrolment Gateway**. It supports standardized enrollment interfaces including CMP, EST, SCEP, REST API, and ACME.

Based on the public product page, the gateway appears to forward requests to **Picocrypt X.509 CA**. That is useful, but it does not appear to be the same as a hospital-side adapter that can route one device enrollment protocol into arbitrary hospital CA infrastructure.

Source: https://picocrypt.de/picocrypt-products/picocrypt-certificate-enrolment-gateway/

### Sectigo Certificate Manager

Sectigo Certificate Manager is a certificate lifecycle management platform. It supports EST endpoints and describes itself as CA agnostic, with support for managing certificates from Sectigo and other public and private CAs, including Microsoft AD CS, Google Certificate Authority Service, and AWS Private CA.

This is close to the broader enterprise certificate-management problem. It appears stronger as an enterprise CLM platform than as a lightweight, vendor-neutral medical-device interoperability target.

Sources:

- https://docs.sectigo.com/scm/scm-administrator/understanding-est-endpoints
- https://www.sectigo.com/products/management-solutions/sectigo-certificate-manager

### Entrust Certificate Enrollment Gateway / PKI Hub

Entrust has a Certificate Enrollment Gateway that supports enrollment and renewal through protocols such as ACMEv2, CMPv2, EST, SCEP, WSTEP, and related mobile-device enrollment flows.

Entrust's broader PKI Hub material also describes integrations with multiple CA types, including Microsoft CA and third-party CA or enrollment gateway integrations.

This is also close to the enterprise PKI/CLM category. It validates that enrollment gateways are a real pattern, but it does not by itself define a medical-device-specific interoperability profile for device manufacturers and hospitals.

Sources:

- https://mobile2.managed.entrust.com/csp/1.2/Certificate-Enrollment-Gateway-overview.html
- https://www.entrust.com/sites/default/files/2024-12/pki-hub-sb.pdf

### OpenXPKI

OpenXPKI is an open-source trustcenter and PKI platform. It supports standard enrollment protocols including SCEP, EST, SimpleCMC, and ACME. It also supports workflow-driven policy and can delegate certificate issuance to external CAs.

OpenXPKI is probably one of the closest open-source conceptual building blocks. It demonstrates that open-source PKI infrastructure can expose standard enrollment protocols and integrate with external CA backends.

The remaining gap is not whether OpenXPKI can issue certificates. The gap is whether the medical-device community has a simple, demonstrable pattern that says: implement this one device-side flow, and hospitals can map it to their own CA choices.

Source: https://www.openxpki.org/

### HashiCorp Vault PKI

HashiCorp Vault Enterprise supports EST for PKI certificate issuance and renewal. Vault is widely used as a secrets and PKI platform, and it can be an internal CA or part of a broader PKI design.

Vault is a powerful backend option, but it is not specifically a medical-device interoperability profile. It could be one CA backend behind a Certificate Enrollment Gateway.

Source: https://developer.hashicorp.com/vault/api-docs/secret/pki/issuance

### Other PKI and CLM Platforms

Other commercial PKI and certificate lifecycle management platforms also support combinations of EST, SCEP, ACME, REST APIs, private CAs, public CAs, certificate discovery, renewal, and deployment automation.

These systems are valuable and should not be ignored. They prove that the industry already recognizes the need for certificate automation and CA abstraction.

## What Existing Solutions Do Not Fully Answer

The core hackathon question is narrower and more medical-device-specific than general certificate lifecycle management.

The question is not:

> Does EST exist?

It does.

The question is not:

> Do PKI and CLM platforms exist?

They do.

The question is:

> Can a medical device manufacturer implement one certificate-enrollment protocol, while each hospital maps that protocol to its own chosen CA infrastructure without custom device-side integration work?

That is the interoperability gap this project is trying to demonstrate.

## Recommended Positioning

The strongest positioning is:

> Existing PKI/CLM systems prove the need for CA abstraction, but they are usually enterprise certificate-management platforms, not a simple medical-device interoperability target. The opportunity is to define and demonstrate a healthcare-focused Certificate Enrollment Gateway pattern: one device-side protocol, many hospital-side CA backends, with activation, renewal, trust removal, auditability, and DICOM transport integration.

This avoids claiming that no related products exist. Related products do exist.

The value of the hackathon is to make the medical-device workflow concrete:

1. A device asks for a certificate.
2. The gateway rejects it until the hospital activates the device.
3. The hospital activates the device.
4. The device enrolls using one standard protocol.
5. The gateway maps that request to the hospital's CA.
6. The device receives a certificate.
7. The device uses that certificate to secure DICOM transport.
8. The device renews automatically.
9. The hospital can remove trust.
10. Transport fails when trust is removed.

## Why This Is Worth Building

This project is worth building because it turns certificate management into an interoperability layer instead of a one-off integration project.

For manufacturers, it reduces the number of hospital-specific certificate integrations they must support.

For hospitals, it preserves their right to choose and operate their own CA infrastructure.

For patients and clinical operations, it reduces the risk of insecure transfer, expired certificates, manual certificate installation mistakes, and inconsistent trust handling across medical devices.

The hackathon should therefore focus on proving the pattern, not on replacing every existing PKI product.

