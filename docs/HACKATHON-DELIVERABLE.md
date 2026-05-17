# Hackathon Deliverable: Certificate-Gated DICOM Transfer

## Vision

The hackathon deliverable is an end-to-end demonstration of a **Certificate Enrollment Gateway** for medical devices.

A Certificate Enrollment Gateway is an intermediary service that lets medical devices request, renew, and validate certificates through a standard protocol such as EST, while abstracting away the hospital's underlying certificate authority infrastructure.

The goal is not to prove one specific product name. The goal is to prove the generic pattern:

> Medical devices integrate once with a Certificate Enrollment Gateway. Hospitals remain free to use different certificate authorities behind that gateway.

This matters because secure DICOM transfer depends on device identity, trust, renewal, and revocation. Today those responsibilities are often manual, inconsistent, or vendor-specific. A gateway gives hospitals one place to control the certificate lifecycle while giving device vendors one standard enrollment path.

## Core Hypothesis

Medical devices can safely exchange DICOM files when certificate issuance is controlled by a Certificate Enrollment Gateway that enforces:

- Device activation before certificate issuance
- Unique device certificates and private keys
- Automated certificate renewal
- Trust removal from an administrator-controlled interface
- Auditability of certificate and device trust events
- CA backend portability without changing the device workflow

## Important Terminology

| Term | Meaning |
|------|---------|
| Certificate Enrollment Gateway | Generic relay/gateway service that exposes EST to devices and connects to one or more CA backends |
| Device | A simulated medical device, PACS, workstation, treatment system, scanner, or other DICOM endpoint |
| EST | Enrollment over Secure Transport, the standard protocol used by devices to request and renew certificates |
| CA Backend | The certificate authority used behind the gateway, such as a self-signed lab CA, Microsoft ADCS, ACME-compatible CA, EJBCA, or another PKI system |
| Activation | Administrator approval that a device is allowed to obtain certificates |
| Untrusted | Administrative state where a device is no longer allowed to obtain or renew certificates |
| DICOM Transfer | A test transfer of one DICOM file between two simulated devices over certificate-protected transport |

## Design Rule

Each device must have its own private key and its own certificate.

Device A and Device B should not share a certificate. What they share is trust in the issuing CA chain and the policy enforced by the Certificate Enrollment Gateway.

## Demo Summary

The demo should show two simulated medical devices exchanging a DICOM file only while both devices are trusted.

The sequence is:

1. Device A tries to enroll before activation and is rejected.
2. An administrator activates Device A.
3. Device A enrolls and receives a certificate.
4. Device B follows the same activation and enrollment process.
5. Device A sends a DICOM file to Device B over certificate-protected transport.
6. Both devices renew their certificates.
7. Device B sends the DICOM file back to Device A.
8. The ping-pong transfer repeats for several rounds using newly issued certificates.
9. During the loop, an administrator marks one device as untrusted.
10. The untrusted device can no longer renew, falls out of trust, and DICOM transfer fails.
11. The same workflow is repeated with another CA backend to prove CA portability.

## Challenge Tracks

The hackathon can support two independent challenge tracks and one combined grand challenge.

| Track | Name | Goal |
|-------|------|------|
| Track 1 | Certificate-Gated DICOM Transfer | Prove that device trust, certificate enrollment, renewal, and untrust can control whether DICOM transfer is allowed |
| Track 2 | DIMSE to DICOMweb Secure Tunnel | Prove that legacy DIMSE services can communicate through a secure HTTP/DICOMweb bridge |
| Grand Challenge | Certificate-Orchestrated DICOMweb Tunnel | Combine both ideas so the DICOMweb tunnel is secured by gateway-issued certificates and governed by device trust policy |

Track 1 and Track 2 can be built separately. The highest-value demonstration is the Grand Challenge because it shows secure transport modernization and certificate lifecycle orchestration working together.

## Phase 1: Enrollment Is Gated

### Goal

Prove that random or unknown devices cannot obtain certificates.

### Flow

1. Simulated Device A generates a keypair and CSR.
2. Device A calls the gateway's EST `/simpleenroll` endpoint.
3. The gateway rejects the request because Device A is not activated.
4. The admin UI shows Device A as pending activation or rejected by policy.
5. An administrator activates Device A.
6. Device A retries enrollment.
7. Device A receives a certificate.
8. The same flow is repeated for Device B.

### Acceptance Criteria

- An unactivated device cannot receive a certificate.
- The administrator can activate a device from the UI or admin API.
- An activated device can receive a certificate.
- The audit trail records both failed and successful enrollment attempts.
- The issued certificate is unique to the device.

## Phase 2: Trusted DICOM Transfer

### Goal

Prove that two approved devices can exchange a DICOM file using certificates issued through the gateway.

### Flow

1. Device A and Device B each hold their own gateway-issued certificate.
2. Device A opens a DICOM transfer connection to Device B using TLS or mutual TLS.
3. Device B validates Device A's certificate chain and trust policy.
4. Device A sends one DICOM file to Device B.
5. Device B accepts the file.

### Acceptance Criteria

- Device A and Device B do not share a private key or certificate.
- Both certificates chain to the expected CA for the selected EST profile.
- The DICOM file transfer succeeds while both devices are trusted.
- The transfer fails if the peer certificate is missing, expired, invalid, or issued by an untrusted CA.
- The demo produces visible evidence: file received, certificate thumbprints, serial numbers, and audit log entries.

## Phase 3: Certificate Ping-Pong Renewal

### Goal

Prove that devices can automatically refresh their certificate bindings as part of repeated secure transfers.

### Flow

1. Device A sends a DICOM file to Device B using its current certificate.
2. After the transfer, Device A requests renewal through EST `/simplereenroll`.
3. Device B also requests renewal through EST `/simplereenroll`.
4. Both devices receive fresh certificates.
5. Device B sends the DICOM file back to Device A using the new certificate state.
6. Repeat for several rounds.

### Acceptance Criteria

- Each transfer uses a valid certificate issued through the gateway.
- Certificate serial numbers or thumbprints change between renewal rounds.
- The gateway records each renewal event.
- Old certificates are marked superseded, expired, or otherwise distinguishable from the current certificate.
- Transfers continue as long as both devices remain trusted.

## Phase 4: Remove Trust During Transfer

### Goal

Prove that an administrator can remove a device from the trusted environment and stop DICOM exchange.

### Flow

1. Start the certificate ping-pong transfer loop.
2. An administrator marks Device B as untrusted in the admin UI.
3. Device B attempts to renew its certificate.
4. The gateway rejects Device B's renewal request.
5. Device B can no longer obtain a current valid certificate.
6. Device A refuses the next DICOM transfer attempt from Device B.

### Acceptance Criteria

- An untrusted device cannot enroll or re-enroll.
- The UI clearly shows the device as untrusted.
- The transfer loop stops because the device is no longer trusted.
- The failed transfer reason is visible and explainable.
- The audit trail records the administrative trust change and the denied certificate request.

## Phase 5: CA Backend Portability

### Goal

Prove that device behavior stays the same even when the hospital changes certificate authority backends.

### Recommended Hackathon Matrix

| CA Backend | Priority | Purpose |
|------------|----------|---------|
| Self-signed lab CA | Required | Fast local proof and repeatable demo |
| Microsoft ADCS | High | Realistic hospital enterprise PKI target |
| ACME-compatible CA | Optional | Proves connector abstraction and public CA compatibility |
| EJBCA, Smallstep, Vault, or other PKI | Stretch | Proves broader enterprise PKI extensibility |

### Acceptance Criteria

- The same simulated devices use the same EST workflow.
- The CA backend can be changed through gateway configuration.
- Device-side enrollment logic does not change when the CA backend changes.
- The DICOM transfer proof still works after changing CA backend.
- The audit trail identifies which CA backend issued each certificate.

## Track 2: DIMSE to DICOMweb Secure Tunnel

### Goal

Prove that a secure HTTP/DICOMweb interface can be placed between two legacy DICOM services.

Many medical systems still communicate using legacy DIMSE networking. DICOMweb provides a modern HTTP-based interface that can be protected with standard web security controls, including TLS, mTLS, certificates, reverse proxies, and firewall policies.

This challenge demonstrates a tunnel:

```text
Legacy DIMSE Device A
    -> Local Bridge A
    -> HTTPS / DICOMweb
    -> Local Bridge B
    -> Legacy DIMSE Device B
```

The legacy systems continue to speak DIMSE at the edges. The middle of the connection moves over secure HTTP using DICOMweb-style messaging.

### Flow

1. Device A sends a DICOM object using DIMSE to Bridge A.
2. Bridge A converts the DIMSE operation into a DICOMweb request.
3. Bridge A sends the DICOMweb request over HTTPS to Bridge B.
4. Bridge B receives the DICOMweb request.
5. Bridge B converts the object back into a DIMSE operation.
6. Device B receives the DICOM object as if it came from a normal DIMSE peer.

### Acceptance Criteria

- Device A and Device B are legacy DIMSE endpoints.
- The bridge layer exposes or consumes a DICOMweb-style HTTP interface between them.
- The HTTP leg is protected with TLS.
- The DICOM object received by Device B matches the object sent by Device A.
- The demo clearly shows where DIMSE ends, where DICOMweb begins, and where conversion back to DIMSE happens.
- The design does not require the legacy devices themselves to implement DICOMweb.

### Stretch Criteria

- Support both send and return transfer.
- Support multiple DICOM files in one run.
- Use mTLS instead of server-only TLS for the DICOMweb leg.
- Log each conversion step with study, series, SOP instance, source device, and destination device.
- Demonstrate firewall rules that allow only the HTTPS tunnel between bridge endpoints.

## Grand Challenge: Certificate-Orchestrated DICOMweb Tunnel

### Goal

Build the Certificate Enrollment Gateway and the DIMSE to DICOMweb tunnel together.

This is the top honor demonstration. It proves that a hospital could modernize transport between legacy DICOM services while centrally controlling certificates and trust.

### Combined Flow

1. Device A and Device B are activated through the Certificate Enrollment Gateway.
2. Bridge A and Bridge B obtain certificates through EST.
3. Device A sends a DIMSE object to Bridge A.
4. Bridge A converts the object to DICOMweb.
5. Bridge A sends the DICOMweb message to Bridge B over mTLS using gateway-issued certificates.
6. Bridge B validates Bridge A's certificate and trust status.
7. Bridge B converts the message back to DIMSE.
8. Device B receives the object.
9. The bridge certificates are renewed through the gateway.
10. An administrator marks Bridge B or Device B as untrusted.
11. The next renewal or tunnel connection fails.
12. The DICOM transfer stops because certificate trust was removed.

### Grand Challenge Acceptance Criteria

- Legacy DIMSE endpoints do not need to know about DICOMweb.
- The bridge-to-bridge leg uses HTTPS or mTLS.
- Bridge certificates are issued and renewed through the Certificate Enrollment Gateway.
- Trust removal in the admin UI prevents the tunnel from continuing.
- The audit trail shows activation, enrollment, renewal, trust removal, and failed secure transfer.
- The same architecture can be explained as a path for securing legacy DICOM traffic without replacing legacy devices.

## Minimum Hackathon Deliverable

The smallest useful deliverable is:

1. A running Certificate Enrollment Gateway.
2. Two simulated devices.
3. Device activation in an admin UI or admin API.
4. EST enrollment rejection before activation.
5. EST enrollment success after activation.
6. Certificate-protected DICOM file transfer from Device A to Device B.
7. Certificate renewal before a return transfer from Device B to Device A.
8. Admin trust removal that prevents the next renewal or transfer.
9. Audit evidence for each major event.

## Deployment Topologies

The demo should be easy to run on a single computer, but the strongest proof is a small real-network deployment.

### Single-Computer Demo

For hackathon development and repeatable demos, it is good if everything can run on one computer.

In this topology:

- Device A is a small console application, script, or lightweight process.
- Device B is a second small console application, script, or lightweight process.
- The Certificate Enrollment Gateway runs locally.
- The CA backend runs locally, either inside the gateway process, as a local service, or in a Docker container.
- The DICOM transfer can happen over localhost or local Docker networking.

This topology is valuable because it makes the proof easy to start, reset, record, and test. It should be the default developer experience.

### Three-Computer Network Demo

The more realistic proof is a computer-to-computer transfer across a real network.

In this topology:

1. Computer 1 runs Device A.
2. Computer 2 runs Device B.
3. Computer 3 runs the Certificate Enrollment Gateway and the CA backend.

This proves the system can work when the devices and gateway are separated by real network boundaries. It also allows the team to demonstrate DNS names, IP addresses, firewall rules, TLS trust, gateway reachability, and device-to-device DICOM transfer outside a single localhost environment.

### Three-Computer Acceptance Criteria

- Device A can reach the gateway's EST endpoint.
- Device B can reach the gateway's EST endpoint.
- Device A and Device B can reach each other only on the intended DICOM transfer port.
- Firewall rules can block unintended traffic while preserving enrollment and DICOM transfer.
- The gateway and CA backend remain centralized on Computer 3.
- Device A and Device B do not need direct access to the CA backend.
- The same activation, enrollment, renewal, untrust, and transfer-failure demo works across the network.

## Stretch Deliverables

- Full ping-pong transfer loop with short-lived certificates.
- Visual dashboard showing device trust state, current certificate, last renewal, and last transfer.
- Certificate revocation support in addition to renewal denial.
- Multiple CA backend demonstrations.
- DICOM association-level validation using a real DICOM networking library.
- Three-computer network demo with documented firewall rules and hostnames.
- Standalone DIMSE to DICOMweb secure tunnel between two legacy DICOM endpoints.
- Grand Challenge demo combining certificate orchestration with the DIMSE to DICOMweb tunnel.
- Exportable demo report showing timestamps, device IDs, certificate serials, and transfer results.

## Success Definition

The demo succeeds when a reviewer can see this sequence without reading code:

1. Unknown device is denied.
2. Admin approves device.
3. Approved device receives a certificate.
4. Two approved devices exchange a DICOM file.
5. Certificates renew automatically.
6. Admin removes trust from one device.
7. That device can no longer renew.
8. DICOM transfer fails because trust was removed.
9. The same gateway pattern can work with more than one CA backend.

For the second challenge, success means a reviewer can see this sequence:

1. A legacy DIMSE sender transmits a DICOM object.
2. A bridge converts that object into a DICOMweb-style HTTPS message.
3. A second bridge receives the HTTP message.
4. The second bridge converts the object back to DIMSE.
5. A legacy DIMSE receiver accepts the object.

For the Grand Challenge, success means the DICOMweb bridge is not just encrypted, but certificate-orchestrated through the Certificate Enrollment Gateway.

## Recommended Demo Script

```text
This is a Certificate Enrollment Gateway for medical devices.

The devices do not talk directly to the certificate authority. They use EST to talk to the gateway.

First, Device A asks for a certificate before it has been activated. The gateway rejects it.

Now the administrator activates Device A. Device A retries and receives a unique certificate.

We repeat that for Device B.

Now Device A sends a DICOM file to Device B over certificate-protected transport. Device B accepts the file because Device A has a trusted certificate.

Before the next transfer, both devices renew their certificates through the gateway. The certificate serial numbers change, proving that the devices are using fresh certificate bindings.

Now Device B sends the file back to Device A.

During the loop, the administrator marks Device B as untrusted. Device B can no longer renew its certificate. On the next transfer attempt, Device A refuses the connection.

This proves that the hospital controls device trust centrally, while the device only needs to implement one standard enrollment workflow.
```
