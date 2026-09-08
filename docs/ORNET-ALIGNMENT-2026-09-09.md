# Wednesday architecture briefing: Kryptonian and OR.NET

Prepared 7 September 2026 for Wednesday, 9 September 2026.
Baseline code reviewed at `dd1a893dbc287111a661e10bd16a2612b1a3a9de`.
The baseline findings below are retained for provenance. Subsequent implementation and
live validation are tracked in [IMPLEMENTATION-TRACKER.md](IMPLEMENTATION-TRACKER.md).
No partner interoperability or clinical conformity certification is claimed.

## Proposed committee decision

Position Kryptonian as an RT-SEC reference implementation and test environment contributing to OR.NET's certificate-enrollment work. Retain EST and the hospital CA gateway. Stop presenting MEDIATE as a separate device protocol or competing specification. Agree common requirements and test cases with OR.NET before defining medical-device-specific conventions.

Our contribution is the operational proof: an imaging device can enroll, renew, use its certificate for DICOM, survive planned rotation, and be denied access when trust is withdrawn. DICOM and SDC retain their existing application protocols.

Suggested spoken opening:

> We have converged on the same enrollment technology. I propose that we contribute our gateway and DICOM lifecycle demonstration to the OR.NET effort, jointly resolve the remaining trust-policy choices, and avoid a second medical-device enrollment specification.

Here, certificate renewal means renewal of cryptographic credentials. It does not recertify the device's clinical safety, regulatory status, or SDC conformity.

## What the sources establish

- Dippel identifies lifecycle management, trust distribution, identity policy, and cryptographic interoperability as unresolved operational issues around SDC. His article is a personal assessment by an OR.NET working-group leader, reviewed by his co-chair; it is not an adopted OR.NET specification. This supports collaboration around PKI without changing BICEPS. [Dippel, April 2026](https://alexdippel.de/2026/04/security-review-of-ieee-11073-sdc-medical-device-standards/)
- The email supplied for this review reports an EST direction, a forthcoming white paper, and possible SDPi integration. Treat these as correspondence-reported working-group intentions. I did not obtain the white paper or independently confirm an agreed enrollment architecture.
- The published SDPi page identifies version 2.4, dated 7 May 2026, as Trial Implementation/Standard for Trial Use. That publication alone does not establish an adopted EST enrollment profile. [Published SDPi](https://profiles.ihe.net/DEV/SDPi/index.html)
- IHE's certificate-provisioning discussion explicitly separates hospital access control, TLS, and vendor-to-vendor conformity trust. Issue 52 is now closed and delegates follow-up work; closure is not evidence that enrollment standardization is complete. [IHE issue 52](https://github.com/IHE/DEV.SDPi/issues/52), [CSR follow-up 198](https://github.com/IHE/DEV.SDPi/issues/198)

## Target architecture

```text
Device or locally attached legacy adapter
  - generates and protects its private key
  - authenticates enrollment service
  - speaks EST; installs and rotates its own credentials
                     |
                  EST/HTTPS
                     |
Hospital enrollment service: Kryptonian or another implementation
  - approves enrollment and binds credential to approved identity
  - selects certificate policy and authorized issuer
  - records lifecycle events and monitors renewal
                     |
              Existing CA connector
                     |
            Hospital-authorized PKI

Certificates are subsequently used on separate application paths:
  DICOM device <-- DICOM over TLS --> DICOM peer
  SDC participant <-- existing SDC secure services --> SDC peer
```

These are small responsibility boundaries, not a proposal for additional microservices. Keep the current application and database where they suffice. The enrollment service should not be required on every clinical message path. A hospital that already has a suitable EST service should be able to use it without deploying Kryptonian.

### Share infrastructure, preserve the meaning of trust

Three questions must remain distinct:

| Question | Evidence and decision owner |
|---|---|
| Which device or participant is this? | Approved identity binding, authenticated bootstrap credential, proof of private-key possession |
| May it participate at this hospital? | Hospital enrollment and local access policy |
| What clinical functions may it perform, and who vouches for them? | Applicable SDC participant policy and accepted manufacturer/conformity authority, plus application authorization |

A hospital certificate must not automatically assert SDC capability or vendor conformity. One enrollment implementation can serve multiple certificate policies, with issuer authority checked for each. Separate certificates/issuers may be appropriate; do not decide a universal one-certificate or two-certificate scheme before reviewing OR.NET's architecture. A shared root must not imply unrestricted communication among all devices.

For the first joint experiment, use separate DICOM and SDC test policies. Preserve SDC's participant identity and role semantics; map inventory identifiers to them rather than imposing `CN=asset-tag` on SDC. Verify exact identity and EKU requirements against the applicable IEEE editions and an actual partner stack before implementing the SDC policy.

### Existing protocols are sufficient for the first experiment

Use EST for enrollment and renewal. An attended onboarding option can use a scoped, expiring credential through EST's existing HTTP authentication over authenticated HTTPS; a provisioned certificate is another option. Select mutually supported authentication with OR.NET. Keep inventory entry and approval in the administration UI. [RFC 7030, sections 2.2 and 3.2.3](https://www.rfc-editor.org/rfc/rfc7030.html)

Pin the EST baseline to RFC 7030 plus applicable updates, including base64 processing in RFC 8951. Use `/csrattrs` when policy must communicate CSR requirements, considering RFC 9908. Its absence is not by itself proof that every EST deployment is invalid. [RFC 8951](https://www.rfc-editor.org/info/rfc8951/), [RFC 9908](https://www.rfc-editor.org/rfc/rfc9908.txt)

Preconfigure the enrollment URL and authenticated trust anchor for the first demo. Fetching `/cacerts` does not, by itself, prove that an initially unknown server is trustworthy. Distinguish enrollment-service trust from application-peer trust, and explicitly test issuer rollover.

If manufacturer-assisted unattended onboarding becomes an agreed requirement, evaluate BRSKI, which already defines manufacturer-assisted bootstrap using device certificates and authorization services. Do not implement a custom discovery/voucher exchange or require BRSKI for the initial attended workflow. [RFC 8995](https://www.rfc-editor.org/rfc/rfc8995.html)

Choose and test a published DICOM TLS profile, initially PS3.15 B.12; assess B.13 only if its additional requirements are supported. Merely enabling TLS 1.2/1.3 does not prove profile conformance. SDC transport selection must be agreed against its own requirements. [DICOM B.12](https://dicom.nema.org/medical/dicom/current/output/chtml/part15/sect_b.12.html), [DICOM B.13](https://dicom.nema.org/medical/dicom/current/output/chtml/part15/sect_b.13.html)

## Code findings that affect the proposal

Paths and line numbers below refer to the reviewed commit. These are source findings, not demonstrated attacks against a running deployment.

| Finding | Evidence | Smallest architectural correction |
|---|---|---|
| Keep the EST and CA-connector foundation | `src/RTSec.Kryptonian.Api/Controllers/EstController.cs:54,95,192`; `src/RTSec.Kryptonian.Domain/Interfaces/ICaConnector.cs` | Reuse enrollment, renewal, CA abstraction, registry and administration. An SDK remains optional. |
| Active-device initial enrollment is not bound to authenticated identity | `src/RTSec.Kryptonian.Application/Services/EnrollmentOrchestrator.cs:124`: device lookup uses CSR CN; only Pending checks the activation credential; Active proceeds to issuance. The supplied authenticated `deviceId` is not an authorization check | Require an authenticated principal-to-device/profile binding on every issuance path. CSR signature proves key possession, not entitlement to another device's name. Exploitability depends on endpoint/authentication configuration. |
| Bootstrap requires private request headers | `src/RTSec.Kryptonian.Api/Controllers/EstController.cs:149`; `src/Kryptonian.MedicalDevice/Enrollment/EstEnrollmentClient.cs:38` | Replace required `X-Activation-Code`/metadata headers in the interoperable path with agreed EST authentication. Collect descriptive metadata administratively. |
| Enrollment trust enforcement has gaps | `src/RTSec.Kryptonian.Domain/Entities/EstProfile.cs:70` uses a public field defaulting false; no explicit mapping was found under Infrastructure. `EstController.cs:329,342` allows skipping chain checks and disables revocation checks. `src/RTSec.Kryptonian.Api/Program.cs:48,355` installs certificate forwarding globally | Persist explicit trust policy, enforce issuer validation, and accept forwarded certificates only through an authenticated, restricted proxy boundary. Verify spoofed headers are rejected on reachable direct paths. Persistence and deployment exposure need runtime checks. |
| Profiles do not reliably select issuers | `EnrollmentOrchestrator.cs:55,165,502` prefers/uses the global active CA; `EstController.cs:287` falls back from an unknown label to the default profile | Bind each policy to its authorized backend and reject unknown labels. Test simultaneous issuers and planned rollover; changing a global backend must not silently move a trust domain. |
| Issuance policy is too weak for shared medical-device profiles | `src/RTSec.Kryptonian.Infrastructure/Crypto/SelfSignedCaConnector.cs:208,237,280` copies requested SANs and supports a fixed list of ordinary EKUs. `EnrollmentOrchestrator.cs:488` checks renewal CN, not complete subject/SAN continuity | Enforce authorized identity, SANs, key parameters, and approved EKUs before issuance; validate returned leaf/key/chain/profile from every adapter. Add SDC OIDs only under authorized SDC policy. |
| Device removal is not peer revocation | `src/RTSec.Kryptonian.Application/Services/DeviceService.cs:147` changes registry status; `SelfSignedCaConnector.cs:130` returns false for revocation; `src/Kryptonian.DICOMTls/CertificateTrustValidator.cs:36` disables revocation checks | Separate enrollment denial, CA revocation, and peer rejection. Use an existing CA's CRL/OCSP capability for the revocation demo, with a stated propagation bound and failure policy. |
| Harness transport is coupled to competition infrastructure | `src/RTSec.Kryptonian.DimseTlsProxy/DimseTlsProxyWorker.cs:102` queries the harness before forwarding to plaintext Orthanc; harness scoring inspects private OIDs in `src/RTSec.Kryptonian.CaHarness/Scoring/DicomVerificationService.cs:68` | Retain scoring as test instrumentation. Interoperability must not require private OIDs, scoring endpoints, or a callback to Kryptonian for each DICOM connection. |
| The proxy's own credential is outside automated renewal | `src/RTSec.Kryptonian.DimseTlsProxy/ServerCertificateProvider.cs:17,25,58` loads or creates a 30-day self-signed PFX with a fixed password; the certificate is retained for the process lifetime | Include adapters and servers in lifecycle scope. Use platform-protected keys and a managed credential, with a tested draining restart or reload on rotation. |
| Renewal support is not yet an unattended device lifecycle | `src/Kryptonian.MedicalDevice/MainWindow.xaml.cs:109,151` invokes renewal from a button; `EstEnrollmentClient.cs:85` treats successful HTTP status as a certificate response without distinguishing pending enrollment | Add a small device-side renewal schedule and tested installation/reload behavior. Handle pending responses and retry timing without adding a private polling protocol. |

The existing renewal implementation does check registered certificate state, profile, and active device status. Preserve those checks while tightening identity continuity. Local approval and certificate registration are legitimate policy choices, not automatically proprietary wire protocols. Existing ADCS/EJBCA adapters are harness-oriented; their presence is not proof of real hospital interoperability. Use a private PKI that can issue the required client/participant certificates; public Web PKI server-certificate compatibility is insufficient evidence.

The plaintext leg of a legacy adapter remains a security boundary. Place it on-device or on an explicitly isolated local segment, prevent bypass, and describe protection as adapter-to-peer where appropriate. It does not make a legacy device natively SDC-capable or provide end-to-end protection through the plaintext leg.

## Work sequence and acceptance evidence

1. **Wednesday: agree direction.** Adopt the positioning above, name an RT-SEC/OR.NET liaison, and agree one shared requirements/test matrix. Bring this architecture and a candid current-state demo; do not claim the proposed fixes are complete.
2. **First engineering slice: ordinary EST interoperability.** Fix principal binding and trusted transport first; remove proprietary bootstrap dependencies; make issuer/profile routing explicit. Prove an independent EST client enrolls and renews without Kryptonian headers, SDK, or administration API credentials. Also prove a device client can use a second EST server with only supported configuration changes.
3. **Second slice: lifecycle proof with DICOM.** Perform a synthetic C-STORE and read back the received SOP Instance UID/content. Rotate the certificate and key, install atomically, and verify new connections use them. Deny renewal after removal. Separately revoke through the CA and measure when peers reject new associations.
4. **Joint slice: real SDC participant.** Ask OR.NET to nominate an existing implementation and approved test identity/role policy. Enroll its credentials through the same EST service and exercise an actual SDC service exchange. Issuing a certificate containing SDC OIDs alone is not an SDC interoperability demonstration.

Keep one compact executable matrix covering: unknown identity; device A requesting B's name; unauthorized SAN/EKU; missing/untrusted credential; spoofed forwarded certificate; activation expiry/replay; unknown profile; renewal/rekey; revoked/expired credential; CA outage; issuer rollover; and peer denial. Use existing tests where they exercise the real boundary. Run independent-client tests across the actual TLS ingress, not only controller mocks.

Availability evidence must include early renewal, retry/backoff, reliable time, atomic installation, and behavior when the enrollment service is unavailable. Existing valid peer credentials should keep working within their policy limits. Define stale revocation-data and established-session handling explicitly; do not promise that revocation instantly terminates an existing association. Choose certificate lifetime from outage tolerance and revocation objectives, not an arbitrary short-lived demo value. Do not silently downgrade to plaintext.

Reuse established audit formats where applicable: map lifecycle and transport events to the relevant ATNA/DICOM audit definitions and protect export with the selected standard transport. This is a separate conformance task, not something a database audit table proves. [IHE ATNA](https://profiles.ihe.net/ITI/TF/Volume1/ch-9.html), [DICOM SYSLOG-TLS](https://dicom.nema.org/medical/dicom/current/output/chtml/part15/sect_A.6.html)

## Questions for the joint discussion

1. Which clinical settings and bootstrap options does the white paper cover, especially offline sites and legacy devices?
2. Which credentials express hospital operational identity versus manufacturer/participant conformity, and who may issue each?
3. What subject/SAN/EKU, cryptographic, renewal, revocation and trust-anchor rollover policies are converging?
4. Is EST enrollment performed by the participant itself or through a management component? Which existing client/server implementations should we test?
5. Where should the shared requirements and test vectors live, and what exact adoption status should each group report publicly?

Proposed division of work: RT-SEC contributes imaging/legacy-device requirements, the CA-adapter experiment, and DICOM evidence; OR.NET contributes its enrollment architectures and SDC policy/participant expertise; both agree the common boundary and exchange tests. This is a proposed collaboration, not a commitment made by OR.NET.

## Review limits

Qwen supplied a bounded code inventory through Herdr; Terra independently reviewed trust boundaries and corroborated the main identity, routing, policy, revocation, forwarding and rotation findings. The parent reviewed the central flows and public sources. No services were started, clinical systems contacted, application code changed, or tests executed for this advice. Full IEEE texts and the unpublished white paper were not available in the reviewed material. This report does not claim EST, SDC, SDPi, ATNA, or DICOM TLS conformance.
