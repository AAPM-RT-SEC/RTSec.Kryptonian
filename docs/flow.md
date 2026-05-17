# Certificate Enrollment Gateway Flow

## Overview

The **Certificate Enrollment Gateway** is the intermediate trust-policy layer between medical devices and hospital certificate authority backends.

The gateway is not just a pass-through relay. It keeps its own view of hospital devices, applies hospital trust policy, selects the correct CA backend, records audit events, and only forwards approved certificate requests to the CA.

The CA backend is still important, but it has a narrower role:

> The CA signs approved certificate requests. The gateway decides whether a device is allowed to ask for a certificate in the first place.

This distinction matters because different hospitals may use different CA systems. A medical device manufacturer should not have to implement custom certificate logic for Microsoft ADCS, ACME, EJBCA, Vault, Sectigo, Entrust, or a private self-signed CA. The device should implement one standard enrollment workflow, such as EST, and the gateway should adapt that workflow to the hospital's CA infrastructure.

## Responsibility Split

```text
Device:
    owns private key
    creates CSR
    requests certificate
    uses certificate for DICOM transport

Certificate Enrollment Gateway:
    tracks device inventory and trust state
    decides activation and trust policy
    selects CA backend/profile
    forwards only approved CSRs
    stores certificate metadata
    audits every enrollment decision

CA Backend:
    signs approved CSRs
    returns CA chain
    may revoke certificates if supported
```

## Trust Policy Role

The gateway acts as the hospital's device trust registry and policy decision point.

It answers questions like:

- Is this device known?
- Has an administrator activated it?
- Is it currently trusted?
- Which CA backend should issue its certificate?
- Which certificate template or profile should apply?
- Is renewal allowed?
- Should this request be rejected before it reaches the CA?
- What audit event should be recorded?

The CA backend usually does not know enough medical-device context to answer those questions consistently across hospitals. The gateway provides that medical-device-specific policy layer.

## Gateway Database

The gateway should keep its own database on top of the CA backend.

### Devices

Example fields:

```text
device_id
display_name
manufacturer
model
serial_number
department
location
status: pending / active / untrusted / retired
assigned_ca_profile
last_seen_at
notes
```

The `status` field is central to trust policy:

- `pending`: device has requested enrollment but has not been approved.
- `active`: device is trusted and allowed to enroll or renew.
- `untrusted`: device is known but no longer allowed to enroll or renew.
- `retired`: device has been removed from service.

### Certificates

Example fields:

```text
device_id
certificate_serial
thumbprint
issuer
not_before
not_after
status: active / superseded / revoked / expired
ca_backend
ca_profile
```

The certificate table lets the gateway show which certificate belongs to which device, which CA issued it, whether it is current, and what was used during a transfer or renewal.

### Enrollment Events

Example fields:

```text
device_id
request_time
source_ip
csr_subject
result: rejected / issued / renewed / blocked
reason
certificate_serial
ca_backend
```

Enrollment events make the demo and production workflow explainable. They answer: who asked, from where, what certificate was requested, what policy decision was made, and why.

## Device Identity

For the hackathon, keep identity simple:

```text
device_id: linac-001
CSR subject: CN=linac-001
```

The gateway can extract the device identity from request metadata, the CSR subject, a header used by the simulator, or a bootstrap credential.

For production, identity should be stronger. Good identity signals include:

- Manufacturer serial number
- Device-provisioned bootstrap token
- Bootstrap certificate installed at manufacturing or hospital onboarding
- TPM or hardware-backed device identity
- Hospital asset tag
- Device model and manufacturer metadata

MAC address and IP address should be supporting metadata only. They should not be the primary trust root.

Reasons:

- MAC addresses are often not visible across routed networks.
- MAC addresses can be spoofed.
- IP addresses may change.
- NAT, proxies, VPNs, and segmented networks can hide or reshape network identity.

They are still useful for inventory and investigation, but they are not enough to prove device identity by themselves.

## Enrollment Flow

```text
Device requests certificate
    -> Gateway checks device trust policy
        -> if pending/unknown/untrusted: reject and audit
        -> if active: forward CSR to selected CA backend
            -> CA signs certificate
    -> Gateway stores certificate record
    -> Gateway returns certificate to device
```

### Step-by-Step Example

1. Device A generates a private key locally.
2. Device A creates a CSR with subject `CN=linac-001`.
3. Device A sends an EST enrollment request to the gateway.
4. The gateway extracts `device_id = linac-001`.
5. The gateway checks its device database.
6. The gateway finds no active device record.
7. The gateway rejects the request before sending anything to the CA.
8. The gateway records an audit event:

```text
device_id: linac-001
source_ip: 10.20.30.40
result: rejected
reason: device not activated
```

9. The admin UI shows a pending device:

```text
device_id: linac-001
first_seen_from: 10.20.30.40
csr_subject: CN=linac-001
status: pending
```

10. An administrator reviews the request.
11. The administrator activates the device and assigns a CA profile:

```text
device_id: linac-001
status: active
assigned_ca_profile: Hospital-ADCS-MedicalDevices
```

12. Device A retries enrollment.
13. The gateway checks the database again.
14. The gateway sees that `linac-001` is active.
15. The gateway forwards the CSR to the selected CA backend.
16. The CA signs the CSR and returns a certificate.
17. The gateway stores certificate metadata:

```text
device_id: linac-001
certificate_serial: 00A1B2C3
thumbprint: 123456...
issuer: Hospital ADCS CA
status: active
```

18. The gateway records an issued enrollment event.
19. The gateway returns the certificate to Device A.
20. Device A uses the certificate for secure DICOM transfer.

## Renewal Flow

Renewal uses the same trust-policy decision.

```text
Device requests renewal
    -> Gateway checks current device status
        -> if active: forward renewal CSR to CA backend
        -> if untrusted/retired: reject and audit
```

This lets the hospital stop future certificate use without requiring every CA backend to implement the same device workflow.

Example:

1. Device A currently has a valid certificate.
2. Device A asks for renewal.
3. Gateway checks `device_id = linac-001`.
4. If status is `active`, renewal is allowed.
5. If status is `untrusted`, renewal is denied before the CA sees the request.

## Trust Removal Flow

Trust removal is also a gateway policy decision.

1. Device A is active and has a certificate.
2. An administrator marks Device A as untrusted.
3. The gateway updates its device database:

```text
device_id: linac-001
status: untrusted
```

4. Device A attempts to renew.
5. The gateway rejects the renewal request.
6. The gateway records an audit event:

```text
device_id: linac-001
result: blocked
reason: device marked untrusted
```

7. Device A can no longer obtain fresh certificates.
8. If the demo uses short-lived certificates, Device A quickly falls out of trust.
9. DICOM transfer fails because Device A can no longer present a current trusted certificate.

## Why This Belongs In The Gateway

Putting trust policy in the gateway gives hospitals a consistent medical-device workflow even when their CA backends differ.

Without the gateway:

```text
Device vendor must adapt to each hospital CA.
Each CA has different APIs, templates, approval rules, and audit behavior.
```

With the gateway:

```text
Device vendor implements one enrollment protocol.
Hospital maps the gateway to its chosen CA backend.
Hospital controls device activation and trust centrally.
```

That is the main role of the intermediate layer:

> It turns certificate issuance into a medical-device trust workflow instead of a one-off CA integration.

