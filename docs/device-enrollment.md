# Device Enrollment

Kryptonian uses a single activation-code enrollment flow. Administrators do not approve a device after it self-registers, and they do not enter the device certificate identity by hand. The administrator creates a pending device alias, the gateway generates a one-time activation code, and the device uses that code as the bootstrap shared secret for its first certificate.

## Roles

- **Administrator**: creates the pending device record and securely delivers the activation code to the real device operator or device provisioning workflow.
- **Device**: generates its own key pair and CSR, presents the activation code, and supplies its device identity details during activation.
- **Gateway**: validates the activation code, binds the device identity to the pending alias, signs the CSR through the currently active CA backend, and returns the first certificate.

## Admin Flow

1. Open the Kryptonian admin UI.
2. Go to the **Devices** tab.
3. Click **Register Device**.
4. Enter only the device **Alias/Nickname**. This is an administrative label, not the certificate subject.
5. Submit the form.
6. The gateway creates a pending device registration and generates a one-time activation code.
7. Copy the activation code and provide it to the device through the hospital's provisioning process.

The admin does not enter the device CN, manufacturer, model, or serial number. Those values come from the device during activation.

## Device Activation Flow

1. The device receives the activation code.
2. The device generates a local key pair.
3. The device creates a CSR containing its requested subject CN.
4. The device calls the gateway activation enrollment endpoint using the activation code and sends device metadata such as:
   - subject CN, from the CSR
   - manufacturer
   - model
   - serial number
5. The gateway validates that the activation code exists, is unexpired, has not been used, and belongs to a pending device registration.
6. The gateway binds the device metadata to that pending registration.
7. The gateway forwards the CSR to the active CA backend using the CA's real protocol implementation.
8. The gateway returns the issued certificate to the device.
9. The gateway marks the device active and consumes the activation code.

Device activation uses EST and must be sent over HTTPS. Local development exposes the admin UI/API on `http://localhost:5000`, but the device-facing EST endpoint is `https://localhost:8443/.well-known/est/simpleenroll`.

For EST simple enrollment, the current activation metadata is carried as:

- `X-Activation-Code`: the one-time activation code
- `X-Device-Manufacturer`: device manufacturer
- `X-Device-Model`: device model
- `X-Device-Serial-Number`: device serial number

The device CN is read from the CSR subject.

## Renewal And Future Signing

After activation, the returned certificate becomes the device's gateway credential. Future CSR signing requests must authenticate with the existing device certificate instead of using the activation code again.

The activation code is only a bootstrap secret for the first certificate. It is not a long-term credential and cannot be reused for renewal.

## Validation Rules

- Activation codes are generated only for pending devices.
- Activation codes are time-limited.
- Activation codes are stored hashed by the gateway.
- Activation codes are consumed after a successful first certificate issuance.
- A device activation must include a CSR subject CN.
- A device activation must include a serial number.
- The requested CN must not already belong to another registered device.
- Removed devices cannot enroll or renew.
- Active devices renew by certificate authentication, not by activation code.
