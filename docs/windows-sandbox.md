# Kryptonian Sandbox for Windows

> **Superseded by version 0.1.1:** use the normal
> [Windows installer instructions](windows-installation.md). Windows Sandbox is
> optional. The old launcher/bootstrap scripts described below were retired;
> this page is retained as a reference for the original 0.1.0 experiment.

An offline, local-only preview of certificate enrollment, renewal, revocation and
synthetic DICOM-over-TLS exchange. Not for clinical use, patient data, or OR.NET/SDC
conformity certification. Successful simulator tests do not validate a real device.

## Run the release (no developer tools)

1. Use an x64 Windows Pro, Enterprise or Education computer with Windows Sandbox
   enabled and hardware virtualization available. Enable **Windows Sandbox** in
   **Turn Windows features on or off**, then restart if requested. Windows Home
   does not provide this feature. Allow at least 8 GB of memory for the sandbox.
2. Extract the complete release folder to a local disk. Keep `KryptonianSandbox.msi`,
   `Launch-Sandbox.cmd`, `Launch-Sandbox.ps1`, and `Sandbox-Bootstrap.ps1` together.
3. Double-click **Launch-Sandbox.cmd**. Do not run the MSI on your normal workstation
   when your intention is a disposable sandbox. The launcher generates a `.wsb`
   file with the correct absolute release-folder path for this computer.
4. Inside Windows Sandbox, installation and service initialization run automatically.
   The browser opens **https://localhost:7445/**. Create your administrator account
   in the dashboard. There is no default username or password.
5. The automatic demonstration configures a local CA/profile, enrolls synthetic
   devices, rejects activation replay, renews with a new key, sends two DICOM TLS
   transfers with read-back, and checks native online certificate revocation.
   Revocation can take up to 90 seconds because of CRL caching. This is not instant.
6. A result summary appears after the run and is saved on the guest desktop, including installation, health, demo and
   restart outcomes. Inspect devices, certificates and events in the dashboard.
   Use the **Run lifecycle demonstration** Start Menu shortcut to repeat the test.
7. Close Windows Sandbox to erase the guest installation, certificates and data.
   Nothing installs on the host. The generated `.wsb` and release files remain.

The browser uses a separate administration CA, explicitly trusted in the guest's
current-user certificate store by this sandbox bootstrap. The device CA is **not**
installed as a Windows trusted root. This trust action never runs on the host.

The launcher disables networking, clipboard sharing and vGPU. Only the release
folder is mapped into the guest, read-only. It does not expose the source repository,
host credentials or clinical files. Re-extract the release into a dedicated folder;
do not place unrelated sensitive files beside the installer.

For automated acceptance evidence, run `Launch-Sandbox.ps1 -CaptureResults` from
Windows PowerShell. This explicitly adds a new writable host folder for a small
sanitized summary. Private keys, credentials, raw logs and DICOM files stay in the
guest. Without that option, all test evidence disappears when the guest closes.

## What is installed

| Item | Location or behavior |
|---|---|
| Application | `C:\Program Files\Kryptonian Sandbox` |
| Windows service | `KryptonianSandbox`, running as LocalService |
| Mutable state | `C:\ProgramData\KryptonianSandbox`, restricted to service/SYSTEM/administrators |
| Administrator dashboard | `https://localhost:7445`, no client-certificate picker |
| EST | `https://localhost:7443/.well-known/est/developer-operational` after demo configuration |
| Signed CRLs | `http://localhost:7444/api/crl/{issuer-SHA256}.crl` |
| Runtime tools | Private bundled .NET and portable PowerShell; no global PATH changes |

All listeners bind to loopback. Admin API routes are not exposed on the EST listener.
The CRL listener accepts only CRL GET/HEAD. The service is demand-start; the Start
Sandbox shortcut initializes protected state and starts it. Restart reuses existing
CA and database. Incomplete key state fails rather than silently replacing trust.

The demo's explicit gateway-CA mode currently skips revocation checking for the
sandbox HTTPS server certificates, which have no CDP, while retaining hostname,
issuer and serverAuth checks. Device and DICOM-peer revocation checks remain enabled.
This documented development exception is not a production configuration.

## Troubleshooting

- **Sandbox cannot start:** verify Windows edition, virtualization, optional feature
  and pending restart. The launcher does not enable OS features or reboot the host.
  On this development host, WindowsSandboxRemoteSession.exe currently fails before
  guest startup with hostfxr.dll access denied (0x80070005 / 0x80008082). This is a
  host Windows Sandbox runtime problem, not an MSI result. Have IT repair/update
  Windows Sandbox; do not change WindowsApps permissions or disable security controls.
- **Installation failed:** inspect `C:\Kryptonian-install.log` inside the guest.
- **Gateway not ready:** inspect `C:\ProgramData\KryptonianSandbox\logs` and Services.
- **Certificate warning:** use the Start Sandbox shortcut, which explicitly trusts
  only this sandbox's administration issuer. Do not bypass certificate errors.
  On a persistent test VM, right-click Start Sandbox / Run lifecycle demonstration
  and choose **Run as administrator**; the demo reads protected test credentials.
- **Demo failed:** inspect the protected demo evidence folder inside ProgramData;
  a failed step is not an interoperability pass. No evidence is automatically deleted.
- **Need more time:** keep the sandbox open. Closing it discards its state permanently.

## Physical medical devices

This release deliberately does not connect physical devices to Windows Sandbox.
Networking is disabled and the listeners are loopback-only. Do not compensate with
host port-forwarding or broad firewall exceptions. Use an isolated lab VM for the
next network-enabled milestone: explicit interface/DNS selection, matching server
SANs, reachable CRL URLs, scoped firewall rules, and vendor-approved trust bootstrap.
Native EST, manual certificate import, and legacy-proxy tests must be reported as
different capabilities. No new device protocol is introduced by this packaging.

## Rebuild the installer (maintainers only)

Requires Advanced Installer 22.0 or later with a suitable license, .NET SDK 10,
Node/npm, PowerShell 7 and build-time Internet access. From the repository root:

```powershell
./installer/windows/Build-Installer.ps1
```

Output: `artifacts/windows-installer/release/`. The checked-in project is
`installer/windows/KryptonianSandbox.aip`. Open it in Advanced Installer for branding
and signing. The payload is regenerated from published binaries and the built UI;
no repository checkout or build tools are needed on the test machine. Portable
PowerShell 7.6.5 is downloaded from Microsoft's release and SHA256-verified.

The DICOMAnon AIP was inspected as a packaging reference but is not modified or
copied wholesale: product/upgrade IDs, application files, Internet prerequisites,
updater and Dropbox publishing actions are deliberately independent.

Preview builds are unsigned unless signing is explicitly configured. Before public
distribution, configure an authorized signing identity, review dependency advisories,
rebuild with supported runtime patches, and test install/repair/upgrade/uninstall in
a fresh VM. Do not ship signing keys or passwords in the AIP, scripts or release.

MSI uninstall removes program files and service, but retains ProgramData state.
The administration trust entry is guest-local and disappears with Windows Sandbox.
On a persistent test VM, remove only that exact issuer manually when retiring the
installation. Back up database, CA and encryption keys together before upgrades.
Do not treat MSI rollback as a database rollback; schema-upgrade testing is required.

## Preview validation (2026-09-08)

- Advanced Installer 22.0 built and validated the MSI; package inspection verified
  the LocalService identity, sandbox arguments, manual start and 1,261 payload files.
- All 65 API tests passed, including ten sandbox bootstrap/boundary cases.
- Published self-contained binaries passed independent EST enrollment, replay
  rejection, native mTLS rekey, forwarded-identity rejection, two DICOM C-STORE
  transfers/read-back, and native online revocation. The packaged demo exited zero.
- The published gateway retained six device records and the same CA across restart.
  Cross-listener admin/EST/CRL isolation was checked with real HTTP requests.
- The DICOMAnon reference AIP remained byte-for-byte unchanged.
- Windows Sandbox guest installation, SCM execution as LocalService, browser setup,
  repair, upgrade and uninstall remain **unverified** because this host's Windows
  Sandbox runtime fails before guest startup. MSI construction and local process
  verification are not substitutes for those acceptance tests.

## Sources

- [Microsoft: Windows Sandbox installation](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-install)
- [Microsoft: Sandbox configuration](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-configure-using-wsb-file)
- [Advanced Installer command line](https://www.advancedinstaller.com/user-guide/command-line.html)
- [PowerShell 7.6.5 release and checksums](https://github.com/PowerShell/PowerShell/releases/tag/v7.6.5)
- [EST, RFC 7030](https://www.rfc-editor.org/info/rfc7030/)
