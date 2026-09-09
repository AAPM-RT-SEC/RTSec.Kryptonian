# Install Kryptonian Gateway on Windows

Use a clean **Windows 11 x64** machine, a lab VM, or an already-running Windows
Sandbox. Windows Sandbox is optional, not a dependency or a required launch method.
This is a local certificate-lifecycle evaluation package, not a clinical release.

## Install and open

1. Copy `KryptonianGateway.msi` onto the target machine (for example its Desktop).
   If using a VM or Windows Sandbox, run it **inside** that machine. Do not launch
   the old `Launch-Sandbox.cmd` or `.wsb` files from version 0.1.0.
2. Double-click the MSI, approve the Windows administrator/UAC prompt, and complete
   installation. The preview is unsigned; use only a package from a source you
   trust. Do not disable organizational security policies to install it.
3. Installation creates the protected data directory, installs the Windows service
   as **LocalService**, starts it, and checks HTTPS health. A readiness failure
   fails installation instead of reporting a working gateway.
4. From the Start Menu, open **Kryptonian Gateway > Open Kryptonian Gateway**.
   This shortcut does **not** require administrator rights. On first use it asks
   whether to trust this installation's separate administration CA for your
   Windows account. Review and accept that explicit prompt to use the browser UI.
   No device-issuing CA is added to Windows trust, and no trust is silently added
   machine-wide or to the installer's SYSTEM account.
5. The dashboard opens at **https://localhost:7445/**. Create your administrator
   account in the first-run wizard. There is no shared default password.
6. Optionally choose **Run lifecycle demonstration** from the same Start Menu group.
   It requests administrator approval because the synthetic test uses protected
   test credentials. It configures the evaluation CA/profile, enrolls devices,
   renews credentials, transfers synthetic DICOM data over mutual TLS, and checks
   revocation. Allow up to 90 seconds for the CRL cache. Repeat runs retain evidence.

The gateway starts automatically with Windows. Normal dashboard use only requires
your application login, not Windows administrator rights. The automatic demo is a
separate optional action; installing does not immediately enroll test devices.

## Prerequisites

- Windows 11 x64, administrator permission to install, and available ports
  **7443, 7444 and 7445**. Close an older development gateway on that target first.
- A browser for the dashboard. No .NET SDK, Node/npm, Docker, Hyper-V, Windows
  Sandbox feature, separately installed PowerShell 7, or external database is needed.
- The MSI bundles self-contained .NET application runtimes, portable PowerShell,
  the compiled dashboard and SQLite support. Installation requires no downloads.

## Which address to use

| Purpose | Address |
|---|---|
| Browser dashboard and administrator API | `https://localhost:7445/` |
| Device EST enrollment | `https://localhost:7443/.well-known/est/` |
| Signed CRLs only | `http://localhost:7444/api/crl/{issuer-SHA256}.crl` |

Do **not** browse port 7443 for administration: it is a device-only TLS listener,
can request a device certificate, and intentionally rejects administrator routes.
Port 7444 intentionally rejects everything except public CRL GET/HEAD requests.
The demo creates the `developer-operational` EST profile under the EST base path.

## If you see "denied"

- **MSI/UAC denied:** an administrator must approve installation. Copy the MSI to
  the target's local disk and run it there. Application control policies can still
  block this unsigned preview; have IT review/sign/allow the package appropriately.
- **Old Start Sandbox shortcut denied:** use version 0.1.1's MSI and Open Kryptonian
  Gateway shortcut. Machine provisioning now happens inside the elevated installer.
- **Browser certificate picker or denied page:** use `https://localhost:7445/`,
  not `:7443`. Use Open Kryptonian Gateway to configure your own browser trust.
- **Application login denied:** create the initial account or use its credentials;
  selecting an unrelated Windows client certificate does not grant administrator access.
- **Installer fails starting the service:** check for occupied ports and the log in
  `C:\ProgramData\KryptonianSandbox\logs`. Do not delete existing CA/database files
  to work around an error. Incomplete key state deliberately fails closed.
- **Demo denied:** accept its UAC prompt. Protected keys and credentials are not
  made world-readable just to allow the demonstration to run.

For a detailed installation log, run from an administrator terminal:

```powershell
msiexec.exe /i "C:\path\KryptonianGateway.msi" /L*v "$env:TEMP\Kryptonian-install.log"
```

For unattended installation, add `/qn /norestart`. No browser/trust prompt runs in
the elevated installer session; each interactive user opens the dashboard shortcut.

## Generate a local CA certificate

Version 0.1.4 automatically exports the public EST root CA after successful
startup to `C:\Users\Public\Documents\Kryptonian-EST-CA.crt` (the Windows Public
Documents folder). Select this file in the device app's **Choose trusted gateway
CA** picker. Installation, upgrade and repair refresh this copy; no private key
is exported and Windows trust is not changed. Ordinary users have read access.

Version 0.1.3 exposes **Allowed Certificate Usages** when adding/editing an EST
profile. For the bidirectional device demo enter
`digitalSignature, clientAuth, serverAuth`. New profiles default to
`digitalSignature, clientAuth`; existing profiles are preserved until edited.
The form blocks saving missing or unsupported operational certificate purposes.

In **CA backends > Add CA backend**, enter a name, choose **selfsigned**, leave
Harness URL blank and select **PFX**. Enter an absolute path such as
`C:\ProgramData\KryptonianSandbox\my-lab-ca.pfx` and a password of at least 12
characters, then choose **Generate new CA PFX**. The directory must already exist
and be writable by the gateway service. Paths are on the gateway computer, not
the browser computer; network paths and junctions are not accepted.

Generation uses .NET (no OpenSSL installation) to create a five-year RSA-3072
self-signed CA with certificate/CRL signing permissions. The encrypted PFX's file
permissions are restricted to the creating service identity, SYSTEM and Windows
administrators. Existing files are never overwritten. Click **Save** afterward
to store the backend configuration; cancelling does not delete the generated file.
If a disk write fails, choose another name and have an administrator inspect the
incomplete file rather than overwriting it.

To use an existing PFX or PEM certificate, enter its existing path and credentials
and save without generating. Generation neither activates a backend nor installs
trust: device trust and EST client-CA configuration must be configured separately
before using a new CA. Keep CA backups and the password secure.

## Files, maintenance and limits

- New installations use `C:\Program Files\Kryptonian Gateway`.
- Private data remains in `C:\ProgramData\KryptonianSandbox`. The internal service
  name remains `KryptonianSandbox` for compatibility with the earlier preview;
  neither that name nor the configuration key requires Windows Sandbox.
- Only the public administration CA is copied to
  `C:\ProgramData\KryptonianGatewayPublic`. Users can read it, not modify it.
- Repair rechecks state/startup. Upgrade keeps the existing CA and database.
  Uninstall removes binaries/service but intentionally retains private state and
  per-user administration trust. Back up database, CA and encryption keys together.
- In Windows Sandbox, closing the guest discards its guest-local installation/state.
  No host-to-guest mappings, port forwards or Windows Sandbox launcher are installed.
- Listeners remain **loopback-only by default**. This package is useful on a physical
  Windows machine or a VM, but enabling external medical-device connections is a
  separate network configuration milestone, not an automatic firewall change.
- Synthetic demo success is not validation of a real device, SDC conformance or
  clinical readiness. The existing test-only HTTPS-server revocation exception
  retains issuer/hostname checks; device/DICOM-peer revocation remains enabled.

## Build and validation

Maintainers use `installer/windows/Build-Installer.ps1` (Advanced Installer 22.0+,
.NET SDK 10, Node/npm and PowerShell 7 on the build machine). Open the independent
`installer/windows/KryptonianGateway.aip` for branding/signing. Default version is
0.1.4; the old product's upgrade identity is retained. DICOMAnon.aip is unchanged.

Output: `artifacts/windows-installer/windows-release/KryptonianGateway.msi` and
`artifacts/windows-installer/KryptonianGateway-0.1.4-win-x64.zip`.
The ZIP contains just the MSI, this guide and SHA256 checksums. Generated runtimes
and installer outputs remain under ignored artifacts, not source control.

Package checks validate the elevated provisioning/readiness actions, their ordering
around service startup, automatic LocalService startup, bundled files and absence
of private state. Version 0.1.2 passes 72 API tests and the production UI build.
Live HTTPS checks verified CA generation, saving/testing the generated backend,
duplicate-path refusal (409) and unauthenticated refusal (401). Browser interaction
has not been verified. The preceding runtime validation passed the complete
EST/DICOM/revocation demonstration and persistence across restart. These are not a
substitute for install/repair/upgrade/uninstall testing on a clean target. The new
MSI's elevated execution and per-user browser flow require target-machine acceptance.
Signing and independent security assessment are still required before public release.

References: [Microsoft custom-action security](https://learn.microsoft.com/en-us/windows/win32/msi/custom-action-security),
[Advanced Installer services](https://www.advancedinstaller.com/installing-windows-services.html).
