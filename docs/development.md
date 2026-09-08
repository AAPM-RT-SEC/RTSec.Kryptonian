# Local developer system

Windows, PowerShell 7, .NET SDK 10 plus the .NET 8 runtime, and Node/npm are required.
No Docker, OpenSSL, OS trust-store changes, or clinical systems are needed.

```powershell
# Terminal 1: builds UI/API and starts foreground gateway; Ctrl+C stops it.
.\scripts\start-dev.ps1
# Terminal 2:
dotnet build src/Kryptonian.DICOMTls
.\scripts\verify-dev.ps1 -CheckRevocation
```

The dashboard is at https://localhost:7443/. The browser warns about the private CA.
Complete the initial administrator wizard for interactive administration. The verifier
uses a separate generated administrator API key and creates no user account.

## Evidence

The verifier creates a named local CA backend/profile and unique synthetic devices.
It exercises independent curl HTTP Basic EST without private headers, generated-key
matching, activation replay rejection, native mTLS renewal with a new key and retained
subject, forwarded-certificate spoof rejection, and two real mutual-TLS DICOM C-STORE
transfers. Received files are reopened and their SOP Instance UIDs compared.

`-CheckRevocation` additionally revokes the renewed credential and requires native
online X509Chain validation to report Revoked, independently of registry lookup.
It allows 90 seconds for the existing 60-second CRL cache to expire; no cache flush.
`-SkipDicom` skips only the DICOM portion. Evidence, encrypted credentials and failed
attempts remain in `artifacts/developer/verify-*/`; synthetic records are not deleted.

Live checks on 2026-09-07 passed enrollment, renewal, two DICOM transfers/read-back,
and native online revocation. A populated SQLite database survived restart. The checks
caught Windows ephemeral-key incompatibility and lost UTC kind in SQLite CRL dates.
See [implementation tracker](IMPLEMENTATION-TRACKER.md).

## State and configuration

| Launcher option | Default | Meaning |
|---|---|---|
| -Port | 7443 | Loopback HTTPS |
| -CrlPort | 7444 | Loopback HTTP; only CRL GET/HEAD allowed |
| -DataDirectory | artifacts/developer | Persistent SQLite, certificates and secrets |
| -NoBuild | off | Reuse existing UI/API output |

Pass matching ports/data directory to the verifier. It configures
`/.well-known/est/developer-operational`; signed CRLs are public at
`http://localhost:7444/api/crl/{issuer-SHA256}.crl`.

Random PFX passwords, JWT secret and admin API key are reused across runs.
The launcher restricts secrets.json to the current Windows user and SYSTEM before
writing secrets. Never commit/share it or PFX files; ca.pem is public.
Existing state is reused, with additive trust/revocation schema upgrades.
PostgreSQL upgrade SQL exists but was not live-tested. Builds use npm ci with no
silent npm install fallback. Environment changes are process-scoped; no other service starts.

## Explicit development exception

The initial gateway SERVER certificate has no CDP. The verifier and the DICOM demo's
explicit --gateway-ca mode skip revocation only for that HTTPS server, retaining CA,
hostname and serverAuth checks. Device/client and DICOM-peer validation use online
revocation. Never carry this exception into clinical deployment or use --insecure.

## Operational proxy and renewal limits

Configure Proxy:ServerCertificatePath, Proxy:ServerCertificatePassword and
Proxy:ClientCaPath (PEM CA bundle). Environment names use double underscores.
Keep passwords out of command-line arguments. Defaults are loopback 4243 to loopback
upstream 4242; ListenAddress, ListenPort, UpstreamHost and UpstreamPort are configurable.

The externally issued server PFX is read on each new connection. Atomically replace it
at the same path; invalid replacements fail closed. The proxy does not mint identities
or keep stale credentials as fallback. Associations have a bounded lifetime, not
instant revocation during an existing connection. Proxy:HarnessMode=true explicitly
enables legacy instrumentation; public certificate download is separately opt-in.

MedicalDevice resumes persisted renewal state and renews after two-thirds of certificate
lifetime while the application runs. It is not an OS service. It installs the new key
before switching the pointer and retains the old certificate for overlap; production
retirement policy is deferred.

The local CA signs CRLs. Unsupported remote backends fail without falsely completing
revocation. Leaves without CDPs need re-enrollment. CA replacement uses a new backend;
retain the old issuer material while old leaves remain in use.

This is operational certificate-lifecycle infrastructure, NOT SDC clinical-role
authorization, DICOM TLS profile certification or readiness for clinical use.
Joint OR.NET profiles, partner tests and an independent security assessment remain necessary.

## Automated checks

```powershell
dotnet test RTSec.Kryptonian.sln
dotnet build src/RTSec.Kryptonian.DimseTlsProxy
cd src/RTSec.Kryptonian.Ui
npm ci
npm run build
```

PR/main CI runs Windows checks. Stop your own gateway before rebuilding its executable
to avoid file locks. Eighteen pre-existing Windows infrastructure skips remain;
new live tests and executing CRL tests are separate evidence.
