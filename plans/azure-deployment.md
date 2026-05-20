# Plan: Push Kryptonian gateway to Azure

## Goal

A publicly reachable Kryptonian gateway that:

- Lives in `rg-kryptonian-hackathon` alongside the existing CA harness.
- Is pre-seeded with all four CA backends (self-signed EST, ADCS SCEP, EJBCA REST, ACME via step-ca) pointing at the existing harness at `https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io`.
- Has one EST profile bound to the active backend so device clients can hit `/.well-known/est/simpleenroll` immediately.
- Hosts the React admin dashboard at the same FQDN.
- Issues device certs to anyone with a valid activation code (no extra auth in front of EST).

## Current Azure state (already verified)

| Resource | Name | Notes |
|---|---|---|
| Subscription | `Red Ion` (`04dd46c9-…`) | active on rexcardan@gmail.com |
| Resource group | `rg-kryptonian-hackathon` | eastus |
| Container Apps env | `kryptonian-cae` | default domain `mangotree-b3d09362.eastus.azurecontainerapps.io` |
| Container Apps | `ca-harness` (public), `step-ca` (internal) | harness responds healthy |
| ACR | `kryptonianregistry.azurecr.io` (Basic SKU) | already serves `ca-harness:v11` |
| Storage / VM / Workspace | `stkryptonianfiles`, `vm-kryptonian-dimse`, `workspace-rgkryptonianhackathonr3hw` | unrelated to gateway deploy |
| Postgres | (none) | will provision if persistence wanted |

Harness `POST /api/teams/register` works publicly (verified with throwaway team `_probe_` → token returned). The harness is the single CA emulator; each team token isolates state but **all four backends share one harness deployment**.

## How the four backends connect

The harness exposes per-team URLs once a team is registered:

| CA type | Gateway backend type | Harness path | Gateway config blob |
|---|---|---|---|
| Self-signed (EST) | `selfsigned` | `/teams/{TEAM}/est/selfsigned/simpleenroll` | `{ "HarnessBaseUrl": "https://ca-harness.…/teams/{TEAM}" }` |
| ADCS (SCEP) | `adcs` | `/teams/{TEAM}/scep/adcs?operation=…` | `{ "HarnessBaseUrl": "https://ca-harness.…/teams/{TEAM}", "TemplateName": "DicomDeviceAuthentication", "ValidityDays": 7 }` |
| EJBCA (REST) | `ejbca` | `/teams/{TEAM}/ejbca/ejbca-rest-api/v1/certificate/pkcs10enroll` | `{ "HarnessBaseUrl": "https://ca-harness.…/teams/{TEAM}", "CertificateProfile": "MedicalDeviceTLS", "EndEntityProfile": "DicomDevice", "ValidityDays": 7 }` |
| ACME (step-ca) | `acme` | `/teams/{TEAM}/acme/directory` | `{ "DirectoryUrl": "https://ca-harness.…/teams/{TEAM}/acme/directory", "Email": "admin@example.com", "PreferredChallengeType": "http-01" }` |

So the gateway needs exactly **one team token** of its own. Register it once, paste it into all four backend configs.

## Issues to fix before the image can run

1. **Dockerfile doesn't build the dashboard.** `src/RTSec.Kryptonian.Api/Program.cs:196` looks for the SPA at `<contentRoot>/../RTSec.Kryptonian.Ui/dist`. The current Dockerfile only `dotnet publish`es. Without changes the deployed gateway will serve the API but return 404 on `/`. Fix: add a `node:22-alpine` build stage that runs `npm ci && npm run build`, then `COPY` the dist into a sibling path inside the runtime image. We'll also widen the API's path lookup so a publish-relative location works (e.g. `<contentRoot>/wwwroot/ui` as a fallback). See "Step 2" below.
2. **Default API key.** Pick a strong key and store it as an ACA secret — anyone with it can manage CAs/profiles via the admin API. EST `/simpleenroll` is anonymous (activation code only), so device enrollers don't need the key.
3. **HTTPS for EST.** The gateway rejects HTTP on `/.well-known/est/*` unless `Kryptonian:Est:AllowPlainHttp=true`. ACA ingress terminates TLS and forwards plaintext to the container, but the request appears HTTPS to the app because ACA sets `X-Forwarded-Proto`. We need `UseForwardedHeaders` enabled (verify) **or** set `AllowPlainHttp=true` in ACA env, since the public boundary is TLS regardless. I'll check before deploying.

## Decisions

| Decision | Choice | Notes |
|---|---|---|
| Persistence | SQLite file on an Azure Files share mounted at `/data` | EF Core SQLite provider; pin `--max-replicas 1` because SQLite + Azure Files locking is only safe with a single writer. |
| Hostname | Default ACA FQDN (`kryptonian-gateway.mangotree-b3d09362.eastus.azurecontainerapps.io`) | ACA managed cert auto-issued. |
| Harness team token | Register once as `kryptonian-collab`, store in ACA secret | Hackathon is now collaborative, not team-based. We still need one token because the harness's URL scheme requires it; "team" is a path-prefix detail, not a competition concept. |
| Deploy mechanism | Bicep template + PowerShell seed script | `deploy/azure/main.bicep` + `deploy/azure/seed-gateway.ps1`, both idempotent. |

## Plan

### Step 0 — Confirmation
Answer the four open questions above.

### Step 1 — Register the collaborative team with the harness (one-time)
```pwsh
$tk = (Invoke-RestMethod -Method POST -Uri https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io/api/teams/register `
  -ContentType 'application/json' -Body '{"teamName":"kryptonian-collab"}').token
```
Stash `$tk` in a Container App secret named `harness-team-token`. (The harness's URL scheme requires a token; this is a mechanical path prefix, not a competition signal.)

### Step 2 — Patch the build pipeline
- Update `src/RTSec.Kryptonian.Api/Dockerfile` so it:
  - Adds a `FROM node:22-alpine AS ui` stage that `npm ci`s and `npm run build`s `src/RTSec.Kryptonian.Ui`.
  - Bakes `VITE_API_KEY` at build time from a Docker build-arg so the dashboard sends `X-API-Key` against the deployed gateway.
  - Copies `dist/` into the runtime stage at `/app/wwwroot/ui` (or keeps the existing `../RTSec.Kryptonian.Ui/dist` layout under `/`).
- Verify / extend `Program.cs:196` so it also looks under the publish output.
- Build + push with ACR build (no local Docker needed):
  ```pwsh
  az acr build -r kryptonianregistry `
    -t kryptonian-gateway:v1 `
    --build-arg VITE_API_KEY=<key> `
    -f src/RTSec.Kryptonian.Api/Dockerfile .
  ```

### Step 3 — Provision SQLite-on-Azure-Files storage
- Reuse the existing `stkryptonianfiles` storage account.
- Create a file share `kryptonian-gateway-data` (5 GiB, hot tier).
- Define an ACA `azureFile` storage entry on the `kryptonian-cae` environment bound to that share.
- Add an EF Core SQLite provider package reference to `RTSec.Kryptonian.Infrastructure.csproj`, gate provider selection on `Kryptonian:Database:Provider` (`Sqlite` | `PostgreSQL` | `InMemory`), and run `Database.Migrate()` at startup when a real provider is configured.
- Wire a SQLite migration set parallel to the existing Postgres migrations (EF migrations are provider-specific).
- Connection string: `Data Source=/data/kryptonian.db;Cache=Shared`.

### Step 4 — Create the Container App (declared in Bicep, summarized here)
- Image: `kryptonianregistry.azurecr.io/kryptonian-gateway:v1` pulled via system-assigned identity (AcrPull role granted in same Bicep).
- Ingress: external, target port 5000.
- Scaling: `min=1`, **`max=1`** (SQLite-on-Azure-Files demands one writer).
- Resources: 0.5 vCPU / 1.0 GiB.
- Volume: `kryptonian-data` (azureFile, share `kryptonian-gateway-data`) mounted at `/data`.
- Secrets: `admin-api-key`, `harness-team-token`.
- Env:
  - `ASPNETCORE_ENVIRONMENT=Production`
  - `ASPNETCORE_URLS=http://+:5000`
  - `Kryptonian__Database__Provider=Sqlite`
  - `ConnectionStrings__DefaultConnection=Data Source=/data/kryptonian.db;Cache=Shared`
  - `Kryptonian__Est__AllowPlainHttp=true` (ACA terminates TLS at ingress)
  - `Kryptonian__AdminApi__ApiKeys=secretref:admin-api-key`
  - `KRYPTONIAN_HARNESS_TEAM_TOKEN=secretref:harness-team-token` (read by the seed script, not by the API itself)

### Step 5 — Seed the four CAs and the EST profile
Run `deploy/azure/seed-gateway.ps1` (to be written) which POSTs:

```
POST /api/cas      ×4    # selfsigned (isActive=true), adcs, ejbca, acme
POST /api/cas/{id}/activate    # selfsigned
POST /api/est-profiles ×1     # name: "Default Device EST", caBackendId: <selfsigned-id>, validityDays: 7, requireClientCertificate: false, hostname: <gateway FQDN>
```
The script reads the team token from the ACA secret so the four `HarnessBaseUrl`/`DirectoryUrl` values point at `/teams/$tk/...`. Idempotent — skips backends that already exist by name.

### Step 6 — Smoke tests
- `curl https://<fqdn>/api/status/health` → 200.
- Browser-load `https://<fqdn>/` → dashboard renders, Devices/CAs pages populated, Settings shows GatewaySettings panel.
- Run `Kryptonian.MedicalDevice.exe` with Gateway = `https://<fqdn>` + manual device fields + activation code (issued via dashboard) → cert installed.
- Verify a backend swap: dashboard → CA Backends → Activate `ejbca` → enroll again → confirm the new cert chains up to a different harness CA.

### Step 7 — Persist the deployment as infra-as-code
- Drop a `deploy/azure/main.bicep` covering ACR pull role assignment + Postgres + Container App + secrets + diagnostics, parameterized on image tag and harness team token.
- Add `deploy/azure/seed-gateway.ps1` + README in `deploy/azure/`.

### Step 8 — Hardening (post-MVP, defer if time-boxed)
- Lock down admin API CIDR (Kryptonian's existing AspNetCoreRateLimit covers EST; admin endpoints want IP-allowlisting or AAD).
- Wire ACA logs to the existing Log Analytics workspace.
- Add a "/about" page (or banner) advertising the gateway as a public test instance and listing the four CA OIDs so external testers know what they're getting.

## Rollback / safety

- Container App is single-replica during seeding so it's easy to delete: `az containerapp delete -g rg-kryptonian-hackathon -n kryptonian-gateway --yes`.
- Postgres has 7-day point-in-time backups by default; teardown also takes one command.
- Harness team token can be deleted via `DELETE /api/admin/teams/{token}` once we have the admin key (Rex has it locally).

## Estimated cost

- Container App (0.5 vCPU / 1 GiB, scale-to-zero possible): **<$10/mo** at low traffic.
- Postgres Burstable B1ms with 32 GiB: **~$13/mo**.
- ACR Basic: already billed.
- Total incremental: **~$20/mo** while running.
