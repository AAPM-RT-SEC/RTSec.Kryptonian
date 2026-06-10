# Demonstrating the ADCS Connector Against a Real Microsoft Backend

This runbook proves that Kryptonian's ADCS connector — today exercised only against the
in-repo CA harness — works unchanged against a **real Microsoft Active Directory Certificate
Services (AD CS)** Enterprise CA, enrolled through **NDES (Network Device Enrollment Service)**
over SCEP.

> **Key fact:** The connector is *not* a mock. [`AdcsCaConnector`](../src/RTSec.Kryptonian.Infrastructure/Harness/AdcsCaConnector.cs)
> and [`ScepClient`](../src/RTSec.Kryptonian.Infrastructure/Harness/ScepClient.cs) build genuine
> SCEP `PKCSReq` (messageType 19) CMS messages, perform `GetCACert`, and parse `CertRep`. The only
> thing "simulated" is the **server** it talks to. Microsoft AD CS exposes the same SCEP protocol
> through NDES. So this demonstration is mostly: stand up real NDES, point the connector at it, and
> close a handful of NDES-specific protocol gaps the harness never exercised.

---

## 1. Scope & success criteria

**Demonstrated when:**

1. The gateway's **Test** button passes against the real NDES URL.
2. A device registered in the gateway enrolls through EST and receives a certificate **issued by the
   real lab Enterprise CA** (verified via the issuer DN and the chain).
3. The issued request appears in the AD CS console under *Issued Certificates*.
4. The DICOM mTLS demo (or the WPF device simulator) completes a real enrollment + renewal using that
   certificate.

**Out of scope:** production hardening, HA NDES, real hospital CA integration, revocation over SCEP
(NDES does not support it; see §6).

---

## 2. Architecture: what changes vs. what stays

```
                          UNCHANGED                          │  CHANGES
 Device ──EST──▶ Kryptonian Gateway ──ICaConnector──▶ AdcsCaConnector ──SCEP──▶  [ NDES server ]
 (WPF sim /      (EST endpoints,                      (ScepClient)               harness  →  real
  DICOM TLS)      device registry,                                               AD CS Enterprise CA
                  approval flow)
```

Only two things move:

- **The backend it points at** — from `http://ca-harness:8080/.../scep/adcs` to
  `https://ndes.lab.local/certsrv/mscep/mscep.dll/pkiclient.exe`.
- **A handful of connector protocol details** (§4) that the harness was lenient about but real NDES
  enforces.

The gateway core, EST flow, device registry, approval workflow, and device simulators are **not
touched** — which is precisely the multi-CA abstraction MEDIATE is meant to prove.

---

## 3. Track A — Stand up the lab (local Hyper-V)

Target: one Windows Server VM acting as Domain Controller + Enterprise CA + NDES. This is the
smallest footprint that produces a *real* Microsoft issuance path. (Splitting DC / CA / NDES onto
separate VMs is more realistic but unnecessary for the demonstration.)

### 3.1 VM

- Hyper-V → New VM, **Windows Server 2022** (2019 also fine), Gen 2, 4 GB RAM, 60 GB disk.
- Internal or Private virtual switch so the host (running the gateway) can reach the VM but the lab
  stays off the real network.
- Note the VM's IP; add a host entry so the gateway can resolve `ndes.lab.local`.

### 3.2 Domain + Enterprise CA

```powershell
# On the VM, as local admin — promote to a throwaway forest
Install-WindowsFeature AD-Domain-Services -IncludeManagementTools
Install-ADDSForest -DomainName "lab.local" -InstallDns -Force
# (reboots)

# AD CS — Enterprise Root CA
Install-WindowsFeature ADCS-Cert-Authority -IncludeManagementTools
Install-AdcsCertificationAuthority -CAType EnterpriseRootCA `
  -CACommonName "Lab Root CA" -KeyLength 2048 -HashAlgorithm SHA256 -Force
```

### 3.3 Device certificate template

In **certtmpl.msc**:

1. Duplicate **Computer** (or **Web Server**) → name it `DicomDeviceAuthentication`.
2. *Extensions → Application Policies*: ensure **Client Authentication** and **Server
   Authentication** EKUs.
3. *Request Handling*: allow the private key to be supplied in the request (SCEP supplies its own
   key); validity 7 days to match the gateway default.
4. *Security*: grant **Enroll** to the NDES service account (created in 3.4).
5. *Subject Name*: **Supply in the request** (the device CSR carries the CN).

Then publish it: **certsrv.msc → Certificate Templates → New → Certificate Template to Issue →
DicomDeviceAuthentication**.

### 3.4 NDES role

```powershell
# Create the NDES service account (domain user) first, e.g. lab\svc-ndes
Install-WindowsFeature ADCS-Device-Enrollment -IncludeManagementTools
Install-AdcsNetworkDeviceEnrollmentService `
  -ServiceAccountName "lab\svc-ndes" -ServiceAccountPassword (Read-Host -AsSecureString) `
  -RAName "Lab NDES RA" -SigningProviderName "Microsoft Strong Cryptographic Provider" `
  -EncryptionProviderName "Microsoft Strong Cryptographic Provider" -Force
```

Point NDES at the device template (registry):

```powershell
# HKLM\SOFTWARE\Microsoft\Cryptography\MSCEP — set all three to the template name
$mscep = "HKLM:\SOFTWARE\Microsoft\Cryptography\MSCEP"
Set-ItemProperty "$mscep\EncryptionTemplate"   -Name Default -Value "DicomDeviceAuthentication"
Set-ItemProperty "$mscep\GeneralPurposeTemplate" -Name Default -Value "DicomDeviceAuthentication"
Set-ItemProperty "$mscep\SignatureTemplate"     -Name Default -Value "DicomDeviceAuthentication"
iisreset
```

> **Demo simplification — challenge password.** NDES defaults to `EnforcePassword=1`: each enrollment
> needs a one-time password from `https://ndes.lab.local/certsrv/mscep_admin/`. For a *repeatable*
> demo you can set `EnforcePassword=0` under `…\MSCEP\` to drop the requirement. Keep it **on** if you
> want to demonstrate the password path (then implement gap #2 in §4). Decide this before the demo —
> it changes whether the connector needs the `ChallengePassword` work.

### 3.5 Capture these values for the gateway

| Value | Where |
|---|---|
| SCEP URL | `https://ndes.lab.local/certsrv/mscep/mscep.dll/pkiclient.exe` |
| Admin / challenge page | `https://ndes.lab.local/certsrv/mscep_admin/` |
| CA chain (root) | Export from certsrv.msc; the host must trust it for TLS |
| Template name | `DicomDeviceAuthentication` |

---

## 4. Track B — Close the connector gaps

These are the deltas between harness-lenient SCEP and real NDES. Gaps **1–3 are mandatory**; 4–5
depend on server hardening and whether you demo the manual-approval template.

### Gap 1 — SCEP path is hardcoded *(mandatory)*

`AdcsCaConnector` posts to `scep/adcs?operation=…`
([:58](../src/RTSec.Kryptonian.Infrastructure/Harness/AdcsCaConnector.cs#L58),
[:108](../src/RTSec.Kryptonian.Infrastructure/Harness/AdcsCaConnector.cs#L108)). Real NDES lives at
`/certsrv/mscep/mscep.dll/pkiclient.exe`. `BaseUrl` only gets a trailing `/`, so the NDES path can't
be absorbed today.

**Fix:** add `ScepPath` to [`AdcsScepConnectorConfig`](../src/RTSec.Kryptonian.Infrastructure/Harness/HarnessConnectorConfig.cs)
(default `/certsrv/mscep/mscep.dll/pkiclient.exe`), use it in both request builders, and wire it
through [`CreateAdcsConnector`](../src/RTSec.Kryptonian.Infrastructure/Crypto/CaConnectorFactory.cs#L195).
Keep the harness default (`scep/adcs`) so existing harness tests stay green.

### Gap 2 — SCEP challenge password *(mandatory unless `EnforcePassword=0`)*

[`ScepClient.BuildPkcsReq`](../src/RTSec.Kryptonian.Infrastructure/Harness/ScepClient.cs#L30) builds
no `challengePassword` attribute (OID `1.2.840.113549.1.9.7`). Default NDES rejects passwordless
requests.

**Fix:** add `ChallengePassword` to config; when present, inject it as a PKCS#9 attribute into the
PKCS#10 before enveloping. (Where the gateway *gets* a fresh per-device password from `mscep_admin`
is a separate question — for the demo, a manually fetched password, or `EnforcePassword=0`, is fine.)

### Gap 3 — GetCACert selects the wrong cert *(mandatory)*

[`GetScepCaCertAsync`](../src/RTSec.Kryptonian.Infrastructure/Harness/AdcsCaConnector.cs#L106) takes
`FirstOrDefault()` from the PKCS#7. NDES returns a **bundle**: root CA **plus** the NDES RA
signing/encryption certs. The enveloped request must be encrypted to the **RA encryption cert**, not
the root CA.

**Fix:** when multiple certs are returned, select the one with a `keyEncipherment` KU (the RA
encryption cert) as the envelope recipient; fall back to the single cert for the harness case.

### Gap 4 — GetCACaps negotiation *(verify against target)*

[`ScepClient`](../src/RTSec.Kryptonian.Infrastructure/Harness/ScepClient.cs#L41) hardcodes
`DesEde3Cbc` + SHA-256. Works on legacy NDES; hardened Server 2019/2022 may refuse 3DES.

**Fix:** query `GetCACaps`; pick AES + SHA-256 when advertised. Also repoint
[`TestConnectionAsync`](../src/RTSec.Kryptonian.Infrastructure/Harness/AdcsCaConnector.cs#L97) from
`/health` (NDES has no such route) to `GetCACaps`.

### Gap 5 — Pending/poll *(only for manual-approval template)*

[`ScepClient.ParseCertRep`](../src/RTSec.Kryptonian.Infrastructure/Harness/ScepClient.cs#L66) treats
any `pkiStatus ≠ 0` as failure. Manual-approval templates return `PENDING(3)`, which requires
`GetCertInitial` polling. Skip if your demo template auto-issues.

> A template-name-in-request note: classic NDES selects the template from the **registry** (§3.4),
> not from the per-request `TemplateName`. The connector already doesn't send `TemplateName` over
> SCEP — so server-side template config is the source of truth. No code change needed; just be aware.

---

## 5. Track C — Run the demonstration & capture evidence

1. **Trust the lab root** on the gateway host so TLS to NDES validates (import the exported root CA).
2. **Add the backend** (gateway admin UI → CA Backends, or REST). With the §4 config keys:

   ```json
   {
     "name": "Lab ADCS (real NDES)",
     "type": "adcs",
     "url": "https://ndes.lab.local",
     "isEnabled": true,
     "config": {
       "ScepPath": "/certsrv/mscep/mscep.dll/pkiclient.exe",
       "TemplateName": "DicomDeviceAuthentication",
       "ChallengePassword": "<from mscep_admin, or omit if EnforcePassword=0>",
       "ValidityDays": 7
     }
   }
   ```

3. **Test** the backend → should pass via `GetCACaps`. Activate it.
4. **Register + enroll a device:** use the `Kryptonian.MedicalDevice` WPF simulator, or run the DICOM
   TLS demo (`dotnet run --project .\src\Kryptonian.DICOMTls\…`).
5. **Capture evidence:**
   - `openssl x509 -in issued.pem -issuer -noout` → issuer is **CN=Lab Root CA** (not the harness CA).
   - The request listed in **certsrv.msc → Issued Certificates** on the VM.
   - The DICOM mTLS association succeeds and prints the real issuer/thumbprint.
   - Screenshots of the gateway dashboard + AD CS console side by side.
6. (Optional) Add a sequence diagram of the real NDES enrollment to `docs/sequence-diagrams/`.

---

## 6. Known limitations to state up front

- **Revocation:** SCEP/NDES has no revoke operation;
  [`RevokeCertificateAsync`](../src/RTSec.Kryptonian.Infrastructure/Harness/AdcsCaConnector.cs#L85)
  already returns `false` and logs. Revocation against AD CS would need a separate channel (certutil /
  CA Web Services) — out of scope for this demo.
- **Per-device challenge passwords** at scale need an automated `mscep_admin` fetch; the demo uses a
  manual password or `EnforcePassword=0`.
- This lab is a throwaway forest, not representative of a hospital's production CA hardening.

---

## 7. Checklist

- [ ] Hyper-V VM: DC + Enterprise CA + NDES installed (§3)
- [ ] `DicomDeviceAuthentication` template published + wired into MSCEP registry
- [ ] Decision recorded: `EnforcePassword` on or off
- [ ] Gap 1 (ScepPath), Gap 2 (ChallengePassword), Gap 3 (RA cert) implemented
- [ ] Gap 4 (GetCACaps + Test fix) — implement or confirm 3DES accepted
- [ ] Gap 5 (pending poll) — only if manual-approval template
- [ ] Harness ADCS tests still green (defaults unchanged)
- [ ] Backend added + Test passes against real NDES
- [ ] Device enrolled; cert chains to **Lab Root CA**; visible in AD CS console
- [ ] DICOM mTLS demo completes with the real cert
- [ ] Evidence captured (chain output + console screenshots)
