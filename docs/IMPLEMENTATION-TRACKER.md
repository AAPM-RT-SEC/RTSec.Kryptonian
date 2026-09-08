# EST alignment implementation tracker

Requested 2026-09-07: individual branches, reviewed PRs and merges, with a working local
developer system. Qwen handled bounded implementation and documentation checks; Terra
handled identity, PKI and harder TLS tests. Parent retained design, review, selective Git
operations and live validation. No clinical deployment or external partner communication.

Each PR below is a separate improvement. GitHub records the exact head, checks and merge
state; merge policy for this run is successful checks plus parent review.

| PR | Improvement | Branch |
|---|---|---|
| [37](https://github.com/AAPM-RT-SEC/RTSec.Kryptonian/pull/37) | Profile issuer routing and unknown-label rejection | fix/est-profile-issuer-routing |
| [38](https://github.com/AAPM-RT-SEC/RTSec.Kryptonian/pull/38) | Authenticated enrollment identity binding | fix/enrollment-identity-binding |
| [39](https://github.com/AAPM-RT-SEC/RTSec.Kryptonian/pull/39) | Trusted forwarding guard | fix/trusted-certificate-forwarding |
| [40](https://github.com/AAPM-RT-SEC/RTSec.Kryptonian/pull/40) | Automatic Windows UI/build/test CI | ci/developer-checks |
| [41](https://github.com/AAPM-RT-SEC/RTSec.Kryptonian/pull/41) | Durable trust policy and additive upgrades | fix/persist-est-trust-policy |
| [42](https://github.com/AAPM-RT-SEC/RTSec.Kryptonian/pull/42) | Standard Basic EST bootstrap; atomic one-time activation | feat/standard-est-bootstrap |
| [43](https://github.com/AAPM-RT-SEC/RTSec.Kryptonian/pull/43) | CSR and returned-certificate policy enforcement | fix/certificate-issuance-policy |
| [44](https://github.com/AAPM-RT-SEC/RTSec.Kryptonian/pull/44) | Pending responses and application-lifetime renewal | feat/device-certificate-renewal |
| [45](https://github.com/AAPM-RT-SEC/RTSec.Kryptonian/pull/45) | Explicit EST client trust and online revocation | fix/est-client-trust |
| [46](https://github.com/AAPM-RT-SEC/RTSec.Kryptonian/pull/46) | Issuer-scoped signed CRLs and transactional revocation | feat/certificate-revocation |
| [47](https://github.com/AAPM-RT-SEC/RTSec.Kryptonian/pull/47) | Partial updates preserve omitted trust lists | fix/profile-partial-update |
| [48](https://github.com/AAPM-RT-SEC/RTSec.Kryptonian/pull/48) | Operational proxy trust and externally rotated PFX | fix/dimse-proxy-trust-lifecycle |
| [49](https://github.com/AAPM-RT-SEC/RTSec.Kryptonian/pull/49) | Reproducible HTTPS/EST/DICOM developer proof | feat/developer-lifecycle-validation |
| [50](https://github.com/AAPM-RT-SEC/RTSec.Kryptonian/pull/50) | Windows Schannel key import and persistent installation | fix/windows-enrollment-key-persistence |
| [51](https://github.com/AAPM-RT-SEC/RTSec.Kryptonian/pull/51) | OR.NET positioning and evidence handoff | docs/ornet-est-alignment |

## Executed evidence, not conformance claims

- Independent curl HTTP Basic EST over real HTTPS; no private enrollment headers.
- One-time activation replay rejected; renewal uses a native TLS client certificate,
  generates a new key and preserves approved identity.
- Direct forwarded-certificate spoof rejected; HTTP listener denies non-CRL requests.
- Two actual mutual-TLS DICOM C-STORE transfers followed by received-file reopen and SOP
  Instance UID comparison. Both peers enforce issuer, TLS purpose and online revocation.
- Administrator revocation followed by native online X509Chain reporting Revoked,
  independently of registry lookup and without flushing the system CRL cache.
- Gateway restart on populated SQLite preserved configured profile, issuer and devices.
- Five loopback proxy tests passed, including unknown/expired/revoked client rejection
  and fresh handshakes observing atomic PFX replacement; expired replacement fails closed.
- Twenty device enrollment/lifecycle tests passed including actual Schannel mTLS with
  the public client's returned certificate; WPF build passed. Developer trust adds a
  separate issuer/hostname/purpose regression.
- Windows GitHub CI builds the UI, tests the solution and builds the standalone proxy.

Combined live-run evidence is local and ignored:
`artifacts/developer/verify-fa867498c35c4ca7bf58c0d4bc3acd21/evidence.json`.
Its two received SOP UIDs are `2.25.154518358314734480382348393848679450333` and
`2.25.281306672341327719638925984637794989584`.
Run [the developer verifier](development.md) to reproduce rather than relying on a screenshot.

Live checks exposed two bugs that mocked tests missed: Schannel rejected ephemeral
attached RSA keys; SQLite's unspecified DateTime kind shifted refreshed CRL revocation
times five hours into the future on this host. Both were fixed with runnable regressions.
Failed attempts remain alongside successful artifacts.

## Deliberate boundaries and follow-up

- No OR.NET-approved clinical identity/role profile or SDC partner participant is present.
  No SDC conformity, adopted EST profile, or DICOM B.12/B.13 certification is claimed.
- The independent EST path uses standard Basic authentication; the existing sample device
  client retains legacy activation headers for compatibility. They are not mandatory for
  other clients and must not become a new interoperability requirement.
- Development HTTPS SERVER revocation is explicitly skipped for the launcher's initial
  server leaf without a CDP. Hostname, issuer and purpose remain checked. Device/client
  and DICOM-peer revocation are enabled. This exception is not a production recommendation.
- Local CA CRLs are demonstrated. Unsupported remote CA revocation fails closed; actual
  ADCS/EJBCA hospital enrollment/revocation interoperability remains unverified.
- Automatic renewal runs only while MedicalDevice is open. Long-lived OS service operation,
  hardware-backed key storage and old-certificate retirement need deployment policy.
- Proxy connections have bounded lifetimes, not instant mid-association revocation.
  PFX renewal is external; proxy reload is demonstrated, not automated CA renewal.
- PostgreSQL schema upgrades are implemented but only SQLite persistence was exercised.
- Eighteen pre-existing Windows infrastructure skips and existing analyzer warnings remain.
  MailKit 4.8.0 still raises moderate advisory GHSA-9j88-vvj5-vhgr; upgrading/testing the
  unrelated email subsystem is a follow-up before any deployment.
- Existing user artifacts and services were preserved. Synthetic demo records remain.

For Wednesday's requested committee decisions and source provenance, use
[the architecture briefing](ORNET-ALIGNMENT-2026-09-09.md). Align operational requirements
and test vectors with OR.NET; do not invent a second protocol or private clinical-role OID.
