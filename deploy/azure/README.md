# Azure deployment — Kryptonian gateway

Single-replica public gateway hosted on the existing
`rg-kryptonian-hackathon` resource group, pre-seeded with four CA backends
pointing at the CA harness and one EST profile so any device with a valid
activation code can enroll right away.

## Files

| File | Purpose |
|---|---|
| `main.bicep` | Provisions the file share, ACA storage binding, container app, and AcrPull role. Idempotent. |
| `seed-gateway.ps1` | POSTs the four `selfsigned`/`adcs`/`ejbca`/`acme` CA backends and one `Default Device EST` profile through the admin API. Skips entries that already exist. |
| `deploy.ps1` | Wrapper: ensures a harness team token, mints (or reuses) an admin API key, runs `az acr build`, runs the Bicep deployment, and runs `seed-gateway.ps1`. |

## First-time deploy

```pwsh
az login                                    # one time
az account set --subscription 'Red Ion'     # if needed
./deploy/azure/deploy.ps1
```

The first run will:

1. Register `kryptonian-collab` with the harness and stash the token in an ACA secret.
2. Generate a strong 256-bit admin API key and stash it in an ACA secret.
3. `az acr build` the gateway image into `kryptonianregistry`.
4. Deploy `main.bicep` (creates the file share, ACA storage binding, container app with system-assigned identity + AcrPull, single replica).
5. Run `seed-gateway.ps1` to create four CA backends, activate the self-signed one, and create one EST profile.

## Re-deploys

```pwsh
./deploy/azure/deploy.ps1                    # rebuild + redeploy
./deploy/azure/deploy.ps1 -SkipBuild         # redeploy current image
./deploy/azure/deploy.ps1 -SkipBuild -SkipSeed   # bicep only
```

Re-runs reuse the existing harness token and admin API key (read from the ACA
secrets), so credentials stay stable across deployments.

## Retrieving credentials

```pwsh
az containerapp secret show `
  -g rg-kryptonian-hackathon -n kryptonian-gateway `
  --secret-name admin-api-key --query value -o tsv
```

`harness-team-token` works the same way. Both are also stored in the team's
private gist (see the team handoff doc).

## Smoke test

```pwsh
$fqdn = az containerapp show -g rg-kryptonian-hackathon -n kryptonian-gateway --query properties.configuration.ingress.fqdn -o tsv
Invoke-RestMethod "https://$fqdn/api/status/health"
```

Then open `https://$fqdn/` in a browser — the dashboard loads, all four CAs
are visible under **CA Backends**, **Default Device EST** is in **EST
Profiles**, and **Settings** shows the gateway defaults panel.

## Architecture notes

- **Storage**: SQLite at `/data/kryptonian.db`, backed by an Azure Files share
  on `stkryptonianfiles`. Single writer — `maxReplicas: 1` is intentional.
- **TLS**: ACA managed cert at the ingress. Internally the app listens on
  port 5000 plain HTTP. `Kryptonian__Est__AllowPlainHttp=true` because the
  TLS boundary is the ingress, not the container.
- **Harness coupling**: every CA backend's `HarnessBaseUrl` is
  `https://ca-harness.…/teams/{token}`, where `{token}` was issued once for
  the `kryptonian-collab` team. Re-running `deploy.ps1` keeps the same token.
- **Admin API**: protected by the API key in `admin-api-key`. The dashboard
  bundle is built with `VITE_API_KEY=<same key>` so authenticated requests
  from the dashboard JS work out of the box.
- **EST enrollment**: anonymous (activation code only), not gated by the
  admin API key.

## Teardown

```pwsh
az containerapp delete -g rg-kryptonian-hackathon -n kryptonian-gateway --yes
az storage share delete --account-name stkryptonianfiles --name kryptonian-gateway-data
az containerapp env storage remove -g rg-kryptonian-hackathon --name kryptonian-cae --storage-name kryptonian-gateway-data
```

The harness team registration on the harness side has no API for self-deletion
without the harness admin key; ask Rex.
