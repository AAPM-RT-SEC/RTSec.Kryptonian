# Portable medical-device enrollment demo

Run `Kryptonian.MedicalDevice.exe` on Windows x64. It is the existing graphical
test-device client, published as a single self-contained executable: no installer,
.NET installation, Node, or administrator launch is required. It is an unsigned
lab preview, not medical-device software for clinical use.

## Prepare the gateway

1. Start the gateway and open its administration dashboard (the Windows package
   uses `https://localhost:7445`). Configure a working CA backend and enabled EST
   profile. Note the profile's complete HTTPS enrollment base URL.
2. Register a test device and generate its activation code. Keep its assigned
   common name and serial number; enter those exact values in the device app,
   together with the registered manufacturer/model.
3. On the demo computer, have the gateway administrator supply the **public root CA
   certificate that issued the EST listener's TLS certificate**. In the device app,
   click **Choose trusted gateway CA...**, select its PEM/CER/CRT file and confirm
   its SHA-256 fingerprint through a trusted channel. This trusts the CA only for
   this app's EST requests; it does not modify Windows certificate stores.
   Do not copy a CA PFX/private key to the device.
   Gateway installer 0.1.4+ supplies this public file automatically in
   `C:\Users\Public\Documents\Kryptonian-EST-CA.crt`; when both applications run
   inside the sandbox, choose that file directly.
   In the default Windows gateway package this is the public `ca.pem`, not
   `admin-ca.pem`; trusting the dashboard's administration CA alone is insufficient.
   If using the packaged sandbox gateway, explicitly select **Lab only: skip EST
   server revocation checks**, because its test HTTPS certificate has no CRL.
   This option is off by default and available only with a selected CA. Hostname,
   chain, server purpose and expiry validation remain enabled; it does not alter
   the gateway's device-certificate revocation policy. Leave it off outside this
   disposable lab scenario. **Use Windows trust** clears the custom selection.

## Demonstrate enrollment

1. Double-click the executable.
2. Enter the EST profile URL, not the administration URL. For the gateway's
   lifecycle-demo profile use
   `https://localhost:7443/.well-known/est/developer-operational`.
   The default `https://localhost:7443` only works when a root EST profile exists.
3. Fill in CN, manufacturer, model, serial and the activation code from the gateway.
4. Select **Install Certificate After Activation** to store the device credential
   in **CurrentUser > Personal** and enable automatic renewal while the app runs.
5. Click **Enroll**. Show the issued certificate's thumbprint in the app, then
   show the device and certificate in the gateway dashboard.

The app generates its device key locally and submits a CSR. The current app still
uses Kryptonian's compatibility activation headers; this demo is not proof of
independent-client EST or IHE conformance.

Automatic renewal starts after two-thirds of certificate lifetime and checks once
a minute while the app is open. It keeps the prior credential for overlap. Its
tracking file is `%LOCALAPPDATA%\Kryptonian\renewal.json`; this app tracks one
installed device credential per Windows user. Expired credentials require a new
attended activation.

Tracked credentials retain a copy of their chosen public CA and revocation option
for automatic renewal, including after restart. Selecting another CA in the form
does not silently change a previously tracked credential's trust. Manual operations
start with Windows trust each launch; select the CA again if needed.

For immediate manual renewal, the **Renew...** button accepts an existing device
PFX. The Activation Code field also serves as its password. The existing export
option writes an **unencrypted** PFX or PEM private key: use only disposable demo
identities, protect/delete exported files appropriately, and prefer the Personal
certificate store for the ordinary walkthrough.

## Connection troubleshooting

- TLS trust/name errors: verify the EST server's issuing CA and use a hostname
  present in its server certificate. The app does not bypass TLS validation.
- 404: check the complete EST profile path and that the profile is enabled.
- 401/403: check the activation code, its expiry/use status, and the device identity.
- Another computer: `localhost` means that computer. The gateway Windows preview
  listens only on loopback by default; external devices need an explicitly
  configured listener, matching server certificate and firewall access.

Single-file describes distribution, not zero disk use: .NET may extract bundled
native components to its per-user temporary cache, and enrolled keys/state are
stored separately. No credentials or gateway-specific trust are bundled.

## Rebuild

```powershell
dotnet publish src/Kryptonian.MedicalDevice -c Release -r win-x64 --self-contained true --artifacts-path artifacts/medical-device/build -o artifacts/medical-device/trusted-ca/win-x64
```

The project already enables single-file publishing, native-library extraction,
compression and embedded debug symbols. Clean-machine GUI enrollment remains a
target-machine acceptance test; a successful publish alone does not prove it.
