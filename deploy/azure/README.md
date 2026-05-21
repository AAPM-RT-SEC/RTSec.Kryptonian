# Azure deployment — Kryptonian gateway

Public gateway hosted in the existing `rg-kryptonian-hackathon` resource group,
pre-seeded with four CA backends pointing at the CA harness and one EST profile
so any device with a valid activation code can enroll right away.

## Architecture

- **API + dashboard**: single ASP.NET Core container app `kryptonian-gateway`
  on the existing `kryptonian-cae` Container Apps environment. ACA terminates
  TLS; internal listener is plain HTTP. `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`
  makes the app honour `X-Forwarded-Proto` from ingress.
- **Database**: Azure Database for PostgreSQL Flexible Server `kryptonian-pg`
  (Burstable B1ms, 32 GiB, PG 16). The schema is created from the EF model via
  `EnsureCreated()` on first boot (versioned migrations are out of the repo
  while the model stabilises — this is documented as a v2 follow-up).
- **CA backends**: every backend's `HarnessBaseUrl`/`DirectoryUrl` points at
  `https://ca-harness.…/teams/{token}/...`, where `{token}` was issued once
  for the `kryptonian-collab` team. Re-running `deploy.ps1` keeps the same
  token.
- **TLS**: ACA managed cert at the ingress, default `*.azurecontainerapps.io`
  FQDN.

## Files

| File | Purpose |
|---|---|
| `main.bicep` | Container app + secrets + AcrPull role assignment. Idempotent. |
| `deploy.ps1` | Wrapper: ensures Postgres + harness token + admin key, runs `az acr build`, runs Bicep, runs `seed-gateway.ps1`. |
| `seed-gateway.ps1` | Idempotently POSTs the four CA backends and one EST profile through the admin API. |
| `smoke-test.ps1` | End-to-end device enrollment probe. |

## First-time deploy

```pwsh
az login                                    # one time
az account set --subscription 'Red Ion'     # if needed
./deploy/azure/deploy.ps1
```

The first run will:

1. Provision Postgres flex server `kryptonian-pg` in **eastus2** (eastus is
   restricted for the Burstable SKU on this subscription as of 2026-05).
   Admin password is generated and stored as ACA secret `pg-admin-password`.
2. Register `kryptonian-collab` with the harness and stash the token in
   ACA secret `harness-team-token`.
3. Mint a 32-byte admin API key, stash in ACA secret `admin-api-key`.
4. `az acr build` the gateway image into `kryptonianregistry`.
5. Deploy `main.bicep` (container app, secrets, AcrPull role).
6. `seed-gateway.ps1` creates the four CA backends + EST profile.

## Re-deploys

```pwsh
./deploy/azure/deploy.ps1                    # rebuild + redeploy
./deploy/azure/deploy.ps1 -SkipBuild         # redeploy current image
./deploy/azure/deploy.ps1 -SkipBuild -SkipSeed   # bicep only
```

Re-runs reuse the existing Postgres password, harness token, and admin API
key (read from the ACA secrets), so credentials stay stable.

## Retrieving credentials

```pwsh
az containerapp secret show -g rg-kryptonian-hackathon -n kryptonian-gateway --secret-name admin-api-key      --query value -o tsv
az containerapp secret show -g rg-kryptonian-hackathon -n kryptonian-gateway --secret-name harness-team-token --query value -o tsv
az containerapp secret show -g rg-kryptonian-hackathon -n kryptonian-gateway --secret-name pg-admin-password  --query value -o tsv
```

All three are also stored in the team's private gist (see the team handoff
doc for the URL).

## Smoke test

```pwsh
./deploy/azure/smoke-test.ps1
```

Should print an `=== Issued certificate ===` block with a `Gateway OID:
1.3.6.1.4.1.99999.1` line — that OID is proof the enrollment travelled
device → gateway → harness rather than going direct.

## Teardown

```pwsh
az containerapp delete -g rg-kryptonian-hackathon -n kryptonian-gateway --yes
az postgres flexible-server delete -g rg-kryptonian-hackathon -n kryptonian-pg --yes
```

The harness team registration on the harness side has no API for self-deletion
without the harness admin key; ask Rex.

## Cost

| Item | Approx. monthly |
|---|---|
| Container App (1 vCPU / 1 GiB, single replica at low traffic) | < $10 |
| Postgres Flex B1ms + 32 GiB | ~$13 |
| ACR Basic | already billed |
| **Total incremental** | **~$20** while running |
