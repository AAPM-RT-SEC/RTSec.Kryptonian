# Admin Setup Guide

This guide lists the exact values to enter in the Kryptonian admin UI for the CA
harness documented at:

`https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/api/help`

Use this guide with your own team token. The examples below use:

```text
{TEAM_TOKEN}
```

Replace that placeholder with the token returned when your team registers with
the harness.

## 1. Get Or Confirm Your Team Token

Register once:

```bash
curl -s -X POST https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/api/teams/register \
  -H "Content-Type: application/json" \
  -d '{"teamName": "Your Team Name"}'
```

Save the returned `token`. It cannot be retrieved later.

The two base URLs used throughout setup are:

| Name | Value |
|---|---|
| Harness base URL | `https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io` |
| Team harness URL | `https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{TEAM_TOKEN}` |

For the harness-backed protocol connectors, use the **Team harness URL** as the
protocol base URL. Kryptonian then appends protocol paths such as `scep/adcs`
and `ejbca/ejbca-rest-api/v1/certificate/pkcs10enroll`. In a hospital install,
these same fields should point at the hospital's ADCS SCEP endpoint or EJBCA
REST base URL, not at a Kryptonian-specific abstraction.

## 2. UI To Use

Use the hackathon React UI in `src/RTSec.Kryptonian.Ui` when possible. It has
guided fields for all four backend types: `selfsigned`, `adcs`, `ejbca`, and
`acme`.

The Blazor admin portal in `src/RTSec.Kryptonian.Web` currently exposes guided
fields for `selfsigned` and `acme`; its CA dialog still labels `adcs` and
`ejbca` as "Not Implemented". If you are using that portal, use the REST API or
the React UI for ADCS and EJBCA setup.

## 3. Global Hackathon Settings

Open **Settings** and enter:

| UI field | Value |
|---|---|
| Harness Base URL | `https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io` |
| Team Token | `{TEAM_TOKEN}` |
| DIMSE Host | `kryptonian-dimse.eastus.cloudapp.azure.com` |
| DIMSE TLS Port | `4243` |
| Orthanc DIMSE Port | `4242` |
| DICOMweb URL | `http://kryptonian-dimse.eastus.cloudapp.azure.com:8042/dicom-web` |
| Called AE Title | `KRYPTONIAN` |
| Bridge AE Title | `KRYPTONIANBRIDGE` |
| Bridge Listen Port | `11112` |
| Proxy Cert Thumbprint | leave blank unless you have pinned the proxy certificate |

The proxy certificate itself can be downloaded from:

```text
http://kryptonian-dimse.eastus.cloudapp.azure.com:8044/server-cert
```

## 4. CA Backends

Open **CA Backends**, select **Add CA Backend**, and create one backend at a
time. Only one backend should be active when you enroll a test device for that
backend.

### Self-Signed

For hackathon scoring, configure the self-signed backend to use the harness EST
endpoint. This gives issued certificates the required EST marker OID.

| UI field | Value |
|---|---|
| Name | `Harness Self-Signed CA` |
| Type | `selfsigned` |
| Primary URL | `https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{TEAM_TOKEN}` |
| PFX Path | leave blank |
| PFX Password | leave blank |
| PEM Certificate Path | leave blank |
| PEM Key Path | leave blank |
| Enabled | checked |
| Activate after save | check this only while testing self-signed enrollment |

The guided self-signed fields are for Kryptonian's local self-signed connector.
Use them only for non-hackathon/local testing where the gateway owns a local CA
certificate and private key. In that mode, leave **Primary URL** blank and use
one of these two local CA configurations.

PFX mode:

| UI field | Value |
|---|---|
| PFX Path | path to the CA PFX visible to the API process, for example `/app/certs/ca.pfx` in Docker or `certs/ca.pfx` locally |
| PFX Password | your CA PFX password |
| PEM Certificate Path | leave blank |
| PEM Key Path | leave blank |

PEM mode:

| UI field | Value |
|---|---|
| PFX Path | leave blank |
| PFX Password | leave blank |
| PEM Certificate Path | path visible to the API process, for example `/app/certs/ca.crt` |
| PEM Key Path | path visible to the API process, for example `/app/certs/ca.key` |

If you must set the harness EST base through advanced JSON instead of Primary
URL, use:

```json
{
  "HarnessBaseUrl": "https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{TEAM_TOKEN}"
}
```

After enrollment, check **Dashboard > Backend Enrollment Evidence** or the
certificate details for the self-signed marker OID
`1.3.6.1.4.1.99999.1`.

### ADCS

This configures the Microsoft AD CS emulation through the harness SCEP endpoint.

| UI field | Value |
|---|---|
| Name | `Harness ADCS SCEP` |
| Type | `adcs` |
| Primary URL | `https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{TEAM_TOKEN}` |
| SCEP Base URL | leave blank when Primary URL is set, or use `https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{TEAM_TOKEN}` |
| Template Name | `DicomDeviceAuthentication` |
| Validity Days | `7` |
| Enabled | checked |
| Activate after save | check this only while testing ADCS enrollment |

Accepted ADCS template values from the harness:

| Template Name | Result |
|---|---|
| `DicomDeviceAuthentication` | issued |
| `DicomBridgeMtls` | issued |
| `PendingApprovalTemplate` | pending |
| `RejectedTemplate` | rejected |

Use `DicomDeviceAuthentication` for normal setup. Successful SCEP enrollments
receive the scoring marker OID `1.3.6.1.4.1.99999.2`.

The connector uses SCEP and talks to these URLs:

```text
GET  https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{TEAM_TOKEN}/scep/adcs?operation=GetCACert
GET  https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{TEAM_TOKEN}/scep/adcs?operation=GetCACaps
POST https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{TEAM_TOKEN}/scep/adcs?operation=PKIOperation
```

### EJBCA

This configures the EJBCA emulation through the harness REST enrollment endpoint.

| UI field | Value |
|---|---|
| Name | `Harness EJBCA REST` |
| Type | `ejbca` |
| Primary URL | `https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{TEAM_TOKEN}` |
| REST Base URL | leave blank when Primary URL is set, or use `https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{TEAM_TOKEN}` |
| Certificate Profile | `MedicalDeviceTLS` |
| End Entity Profile | `DicomDevice` |
| Issuer DN | leave blank unless you need EJBCA REST revocation |
| Validity Days | `7` |
| Enabled | checked |
| Activate after save | check this only while testing EJBCA enrollment |

Accepted EJBCA certificate profile values from the harness:

| Certificate Profile | Result |
|---|---|
| `MedicalDeviceTLS` | issued |
| `DicomWebBridgeMTLS` | issued |
| `RejectedProfile` | rejected |

Use `MedicalDeviceTLS` for normal device certificates. Successful EJBCA REST
enrollments receive the scoring marker OID `1.3.6.1.4.1.99999.3`.

The connector uses EJBCA REST and talks to this URL:

```text
POST https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{TEAM_TOKEN}/ejbca/ejbca-rest-api/v1/certificate/pkcs10enroll
```

### ACME

This configures the harness ACME proxy in front of step-ca.

| UI field | Value |
|---|---|
| Name | `Harness ACME step-ca` |
| Type | `acme` |
| Primary URL | leave blank, or use the same value as Directory URL |
| Directory URL | `https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{TEAM_TOKEN}/acme/directory` |
| Account Email | your team/admin email, for example `admin@example.com` |
| Challenge Type | `http-01` |
| EAB Key ID | leave blank |
| EAB HMAC Key | leave blank |
| Enabled | checked |
| Activate after save | check this only while testing ACME enrollment |

Important ACME values:

| Item | Value |
|---|---|
| ACME order domain / CSR CN or SAN | `ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io` |
| Challenge relay URL | `https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{TEAM_TOKEN}/acme/challenge` |
| ACME claim URL | `https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/teams/{TEAM_TOKEN}/api/backends/acme/claim` |

The harness can only answer HTTP-01 challenges for its own public hostname, so
the ACME certificate request must be for
`ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io`.

## 5. EST Profile

Open **EST Profiles**, select **Add EST Profile**, and create a device-facing
profile for the active backend.

Recommended general profile:

| UI field | Value |
|---|---|
| Name | `Default Device EST` |
| CA Backend | select the backend you are testing |
| Hostnames | `localhost, ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io` |
| Hostname Match Type | `exact` |
| Path Prefix | `/.well-known/est` |
| Certificate Validity (Days) | `7` |
| Certificate Template | leave blank unless testing a CA-specific template |
| Require Client Certificate | off for initial enrollment; on only for renewal workflows |
| Validate Client Certificate Chain | off unless you have already configured the trusted client CA chain |
| Enabled | checked |

For ACME testing, the CSR must contain a valid DNS name. Use this stricter
profile when ACME is active:

| UI field | Value |
|---|---|
| Hostnames | `ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io` |
| Hostname Match Type | `exact` |

## 6. Test Sequence

For each backend:

1. Open **CA Backends**.
2. Click **Activate** on the backend you want to test.
3. Open **Devices**.
4. Register a device.
5. Approve the device.
6. Enroll the device through the active backend.
7. Confirm **Dashboard > Backend Enrollment Evidence** shows a successful cert
   for the backend.

Expected backend evidence:

| Backend | Protocol used by harness | Expected marker |
|---|---|---|
| `selfsigned` | EST simpleenroll | `1.3.6.1.4.1.99999.1` |
| `adcs` | SCEP PKIOperation | `1.3.6.1.4.1.99999.2` |
| `ejbca` | EJBCA REST pkcs10enroll | `1.3.6.1.4.1.99999.3` |
| `acme` | ACME directory | ACME-issued certificate; score is claimed with the ACME claim endpoint |

For Flow 1 DIMSE mTLS testing, use:

| Field | Value |
|---|---|
| DIMSE TLS proxy | `kryptonian-dimse.eastus.cloudapp.azure.com:4243` |
| Called AE Title | `KRYPTONIAN` |
| Proxy server cert | `http://kryptonian-dimse.eastus.cloudapp.azure.com:8044/server-cert` |

For plain Orthanc DIMSE and DICOMweb:

| Service | Value |
|---|---|
| Orthanc plain DIMSE | `kryptonian-dimse.eastus.cloudapp.azure.com:4242` |
| Orthanc DICOMweb | `http://kryptonian-dimse.eastus.cloudapp.azure.com:8042/dicom-web` |
