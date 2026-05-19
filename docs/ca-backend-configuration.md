# CA Backend Configuration

This guide shows how to add each CA backend supported by the MEDIATE gateway.

Medical devices do not choose a CA backend. Administrators configure one or more backends, then mark one backend active. Future EST enrollments are routed to the active backend.

## Supported Backends

The gateway currently implements these backend types:

| Type | Connector | Status |
|---|---|---|
| `selfsigned` | Local self-signed CA certificate | Implemented |
| `acme` | ACME CA using Certes | Implemented |
| `adcs` | ADCS SCEP-compatible external service | Implemented |
| `ejbca` | EJBCA REST-compatible external service | Implemented |

These types exist in the enum but are not implemented yet: `cfssl`, `hashicorpvault`, `smallstep`, and `openxpki`.

## Adding A Backend In The UI

Open **CA Backends**, then choose **Add CA Backend**.

Every backend has these common fields:

| Field | Meaning |
|---|---|
| `Name` | Operator-friendly display name. |
| `Type` | One of `selfsigned`, `acme`, `adcs`, or `ejbca`. |
| `Primary URL` | Optional backend URL. Some connectors use it as a fallback if the typed config field is blank. |
| `Enabled` | Backend can be tested or activated. |
| `Activate after save` | Makes this the active CA backend for future enrollments. |
| `Advanced JSON config` | Raw config object. The guided fields write these same JSON properties. |

## Self-Signed Backend

Use this when the gateway signs certificates using a local CA certificate and private key.

### Guided Fields

| Field | JSON property | Required | Notes |
|---|---|---:|---|
| `PFX Path` | `PfxPath` | Required if not using PEM | Path to a PFX containing the CA cert and private key. |
| `PFX Password` | `PfxPassword` | Required with `PfxPath` | Password for the PFX. |
| `PEM Certificate Path` | `CertPath` | Required if not using PFX | Path to the CA certificate PEM. |
| `PEM Key Path` | `KeyPath` | Required if not using PFX | Path to the CA private key PEM. |

You must provide either:

- `PfxPath` and `PfxPassword`
- or `CertPath` and `KeyPath`

### JSON Example

```json
{
  "PfxPath": "certs/dev-ca.pfx",
  "PfxPassword": "changeit"
}
```

or:

```json
{
  "CertPath": "certs/dev-ca.crt",
  "KeyPath": "certs/dev-ca.key"
}
```

### REST Example

```powershell
Invoke-RestMethod -Method Post `
  -Uri "http://localhost:5000/api/cas" `
  -ContentType "application/json" `
  -Body '{
    "name": "Local Self-Signed CA",
    "type": "selfsigned",
    "url": null,
    "isEnabled": true,
    "isActive": true,
    "config": {
      "PfxPath": "certs/dev-ca.pfx",
      "PfxPassword": "changeit"
    }
  }'
```

### Environment Fallbacks

If the JSON property is not set, the connector checks:

- `KRYPTONIAN__CA__SELFSIGNED__CERTPATH`
- `KRYPTONIAN__CA__SELFSIGNED__KEYPATH`
- `KRYPTONIAN__CA__SELFSIGNED__PFXPATH`
- `KRYPTONIAN__CA__SELFSIGNED__PFXPASSWORD`

## ADCS Backend

Use this for the ADCS SCEP connector.

### Guided Fields

| Field | JSON property | Required | Default |
|---|---|---:|---|
| `Gateway Harness Base URL` | `HarnessBaseUrl` | Required unless `Primary URL` is set | none |
| `Template Name` | `TemplateName` | No | `DicomDeviceAuthentication` |
| `Validity Days` | `ValidityDays` | No | `7` |

The connector resolves the base URL in this order:

1. `config.HarnessBaseUrl`
2. `url`
3. `KRYPTONIAN__CA__HARNESS__BASEURL`

### JSON Example

```json
{
  "HarnessBaseUrl": "https://ca.example.local",
  "TemplateName": "DicomDeviceAuthentication",
  "ValidityDays": 7
}
```

### REST Example

```powershell
Invoke-RestMethod -Method Post `
  -Uri "http://localhost:5000/api/cas" `
  -ContentType "application/json" `
  -Body '{
    "name": "ADCS SCEP",
    "type": "adcs",
    "url": "https://ca.example.local",
    "isEnabled": true,
    "isActive": false,
    "config": {
      "TemplateName": "DicomDeviceAuthentication",
      "ValidityDays": 7
    }
  }'
```

## EJBCA Backend

Use this for the EJBCA REST connector.

### Guided Fields

| Field | JSON property | Required | Default |
|---|---|---:|---|
| `Gateway Harness Base URL` | `HarnessBaseUrl` | Required unless `Primary URL` is set | none |
| `Certificate Profile` | `CertificateProfile` | No | `MedicalDeviceTLS` |
| `End Entity Profile` | `EndEntityProfile` | No | `DicomDevice` |
| `Validity Days` | `ValidityDays` | No | `7` |

The connector resolves the base URL in this order:

1. `config.HarnessBaseUrl`
2. `url`
3. `KRYPTONIAN__CA__HARNESS__BASEURL`

### JSON Example

```json
{
  "HarnessBaseUrl": "https://ca.example.local",
  "CertificateProfile": "MedicalDeviceTLS",
  "EndEntityProfile": "DicomDevice",
  "ValidityDays": 7
}
```

### REST Example

```powershell
Invoke-RestMethod -Method Post `
  -Uri "http://localhost:5000/api/cas" `
  -ContentType "application/json" `
  -Body '{
    "name": "EJBCA REST",
    "type": "ejbca",
    "url": "https://ca.example.local",
    "isEnabled": true,
    "isActive": false,
    "config": {
      "CertificateProfile": "MedicalDeviceTLS",
      "EndEntityProfile": "DicomDevice",
      "ValidityDays": 7
    }
  }'
```

## ACME Backend

Use this for ACME-compatible CAs.

### Guided Fields

| Field | JSON property | Required | Default |
|---|---|---:|---|
| `Directory URL` | `DirectoryUrl` | Required unless `Primary URL` is set | Let's Encrypt production directory |
| `Account Email` | `Email` | Yes | none |
| `Challenge Type` | `PreferredChallengeType` | No | `http-01` |
| `EAB Key ID` | `EabKeyId` | Required only when CA uses EAB | none |
| `EAB HMAC Key` | `EabHmacKey` | Required only when CA uses EAB | none |

Supported challenge types:

- `http-01`
- `dns-01`

If one EAB value is provided, both `EabKeyId` and `EabHmacKey` must be provided.

The connector resolves the directory URL in this order:

1. `config.DirectoryUrl`
2. `url`
3. `KRYPTONIAN__ACME__DIRECTORYURL`
4. Let's Encrypt production directory

### JSON Example

```json
{
  "DirectoryUrl": "https://acme-staging-v02.api.letsencrypt.org/directory",
  "Email": "device-admin@example.com",
  "PreferredChallengeType": "http-01"
}
```

With External Account Binding:

```json
{
  "DirectoryUrl": "https://acme.zerossl.com/v2/DV90",
  "Email": "device-admin@example.com",
  "PreferredChallengeType": "http-01",
  "EabKeyId": "your-eab-key-id",
  "EabHmacKey": "your-eab-hmac-key"
}
```

### REST Example

```powershell
Invoke-RestMethod -Method Post `
  -Uri "http://localhost:5000/api/cas" `
  -ContentType "application/json" `
  -Body '{
    "name": "ACME Staging",
    "type": "acme",
    "url": null,
    "isEnabled": true,
    "isActive": false,
    "config": {
      "DirectoryUrl": "https://acme-staging-v02.api.letsencrypt.org/directory",
      "Email": "device-admin@example.com",
      "PreferredChallengeType": "http-01"
    }
  }'
```

### Environment Fallbacks

If the JSON property is not set, the connector checks:

- `KRYPTONIAN__ACME__DIRECTORYURL`
- `KRYPTONIAN__ACME__EMAIL`
- `KRYPTONIAN__ACME__EABKEYID`
- `KRYPTONIAN__ACME__EABHMACKEY`
- `KRYPTONIAN__ACME__CHALLENGETYPE`

## Activating A Backend

Only one backend is active at a time.

From the UI, click **Activate** on the backend row.

From REST:

```powershell
Invoke-RestMethod -Method Post `
  -Uri "http://localhost:5000/api/cas/{backendId}/activate"
```

Activating a backend affects future certificate enrollments only. Existing device records and issued certificates remain available.

## Testing A Backend

From the UI, click **Test**.

From REST:

```powershell
Invoke-RestMethod -Method Post `
  -Uri "http://localhost:5000/api/cas/{backendId}/test"
```

The response is:

```json
{
  "success": true
}
```

## Common Setup Sequence

1. Add at least one CA backend.
2. Activate one enabled backend.
3. Add an EST profile.
4. Register or receive a device approval request.
5. Approve the device.
6. The device enrolls through EST.

At enrollment time the gateway selects the active CA backend. The device never sends a backend id.
