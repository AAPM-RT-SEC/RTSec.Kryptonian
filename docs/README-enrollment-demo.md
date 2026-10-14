# Kryptonian enrollment demo — Windows x64

This ZIP contains `KryptonianGateway.msi` and `Kryptonian.MedicalDevice.exe`.
Extract both into a clean Windows machine or an already-running Windows Sandbox.
No developer tools or separately installed .NET runtime are needed.

1. **Install Kryptonian Gateway.** Run the MSI and approve the administrator
   prompt. Open **Kryptonian Gateway > Open Kryptonian Gateway** from Start,
   accept the administration CA prompt after reviewing it, and create your admin
   account. The dashboard is `https://localhost:7445`.
2. **Create a self-signed CA backend.** Under **CA Backends**, add a backend named
   `test`, choose `selfsigned`, leave Harness URL blank, and select PFX. Enter
   `C:\ProgramData\KryptonianSandbox\demo-ca.pfx` and a password of at least 12
   characters. Click **Generate new CA PFX**, enable/activate the backend and Save.
   Use a new filename if that file already exists.
3. **Create an EST profile.** Use name `test`, hostname `localhost`, path prefix
   `/.well-known/est`, and validity of 1 day for this demonstration. In **Allowed
   Certificate Usages**, enter `digitalSignature, clientAuth, serverAuth`. Enable
   the profile and Save. If using a different path, include it in the device URL.
4. **Create a device and copy its activation code.** Register a test device in
   the gateway and generate its activation code. Note its assigned CN and serial.
5. **Run `Kryptonian.MedicalDevice.exe`.** No installer is needed for this app.
   Set the gateway URL to `https://localhost:7443/.well-known/est`.
6. **Choose the gateway CA.** Click **Choose trusted gateway CA...** and select
   `C:\Users\Public\Documents\Kryptonian-EST-CA.crt`, supplied by the installer.
   Confirm the fingerprint against the gateway's public certificate. This selects
   trust for the device app; it does **not** install a Windows root certificate.
   Do not export `localhost.crt` from the browser or select `admin-ca.pem`.
7. **Check the lab box.** Enable **Lab only: skip EST server revocation checks**
   for this disposable sandbox gateway, whose test server certificate has no CRL.
   Hostname, issuer and certificate-expiry checks remain enabled.
8. **Enter the device details.** Fill in CN, manufacturer, model and serial. You
   can use `test` for each in a fresh demonstration, provided they match the
   device registered in step 4. Use the exact assigned CN if the gateway generated it.
9. **Paste the activation code** from step 4 into **Activation Code**.
10. **Install and enroll.** Check **Install Certificate After Activation** (the
    install-after-enrollment option), then click **Enroll**. The app displays the
    issued certificate's thumbprint and installs it in the current user's Personal
    certificate store. Return to the gateway to show the device and issued certificate.

## Quick troubleshooting

- **SSL error:** verify the selected CA and lab checkbox. The browser dashboard
  and EST enrollment server use different CAs and ports.
- **400 certificate-purpose error:** edit the EST profile's Allowed Certificate
  Usages to the three values in step 3.
- **404:** the device URL must match the EST profile's path.
- **Expired/used activation code:** generate a fresh code in the gateway.
- Run both applications inside the same sandbox for this walkthrough. `localhost`
  does not refer to the host computer; the gateway package listens locally by default.

This is an **unsigned evaluation preview**, not a clinical release. Do not bypass
organizational application-control policies. Never share CA private keys or use
patient data for this demonstration. Closing Windows Sandbox discards its state.
