#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Deploy the Kryptonian gateway to Azure end-to-end.

.DESCRIPTION
    Idempotent sequence:
      1. (optional) az acr build — builds and pushes the image. Skip with -SkipBuild.
      2. Ensure Postgres flexible-server `kryptonian-pg` exists in the resource group.
         The admin password is generated on first creation and saved as the
         `pg-admin-password` ACA secret on the container app for retrieval.
      3. Ensure a harness team token exists (registers `kryptonian-collab` if needed).
      4. Mint or reuse an admin API key.
      5. Deploy deploy/azure/main.bicep into rg-kryptonian-hackathon, passing
         the Postgres connection string as a secure parameter.
      6. Run deploy/azure/seed-gateway.ps1.

.PARAMETER ImageTag
.PARAMETER ResourceGroup
.PARAMETER SkipBuild     Skip ACR build; deploy whatever tag is already in ACR.
.PARAMETER SkipSeed      Skip the post-deploy seed step.
.PARAMETER PostgresLocation  Region for the Postgres flex server. eastus2 is the default because eastus is restricted for the Burstable SKU on this subscription.

.EXAMPLE
    ./deploy/azure/deploy.ps1
    ./deploy/azure/deploy.ps1 -SkipBuild      # redeploy current image
#>

param(
    [string]$ImageTag = 'v2',
    [string]$ResourceGroup = 'rg-kryptonian-hackathon',
    [string]$Registry = 'kryptonianregistry',
    [string]$HarnessBaseUrl = 'https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io',
    [string]$PostgresServer = 'kryptonian-pg',
    [string]$PostgresLocation = 'eastus2',
    [string]$PostgresDb = 'kryptonian',
    [string]$PostgresUser = 'kryptonian',
    [switch]$SkipBuild,
    [switch]$SkipSeed
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..' '..')
$bicepFile = Join-Path $PSScriptRoot 'main.bicep'
$seedScript = Join-Path $PSScriptRoot 'seed-gateway.ps1'

function New-RandomString {
    param([int]$Bytes = 24)
    $b = [System.Security.Cryptography.RandomNumberGenerator]::GetBytes($Bytes)
    return ([Convert]::ToBase64String($b) -replace '\+', '-' -replace '/', '_' -replace '=', '')
}

function Get-ContainerAppSecret {
    param([string]$Name)
    $existing = az containerapp secret list -g $ResourceGroup -n kryptonian-gateway --query "[?name=='$Name']" -o json 2>$null
    if ($existing -and ($existing | ConvertFrom-Json).Count -gt 0) {
        return (az containerapp secret show -g $ResourceGroup -n kryptonian-gateway --secret-name $Name --query value -o tsv 2>$null).Trim()
    }
    return $null
}

# 1. Ensure Postgres flex server exists.
$pgFqdn = az postgres flexible-server show -g $ResourceGroup -n $PostgresServer --query fullyQualifiedDomainName -o tsv 2>$null
$pgPass = Get-ContainerAppSecret 'pg-admin-password'

if (-not $pgFqdn) {
    Write-Host "Provisioning Postgres flexible-server '$PostgresServer' in $PostgresLocation..."
    if (-not $pgPass) { $pgPass = (New-RandomString) + 'A1!' }
    az postgres flexible-server create `
        --resource-group $ResourceGroup `
        --name $PostgresServer `
        --location $PostgresLocation `
        --tier Burstable `
        --sku-name Standard_B1ms `
        --storage-size 32 `
        --version 16 `
        --admin-user $PostgresUser `
        --admin-password $pgPass `
        --public-access 0.0.0.0 `
        --database-name $PostgresDb `
        --yes -o none
    $pgFqdn = az postgres flexible-server show -g $ResourceGroup -n $PostgresServer --query fullyQualifiedDomainName -o tsv
    Write-Host "  Postgres FQDN: $pgFqdn"
} else {
    Write-Host "Reusing existing Postgres server: $pgFqdn"
    if (-not $pgPass) {
        throw "Postgres server '$PostgresServer' exists but no 'pg-admin-password' secret is on the container app. Reset the admin password manually and re-run with --SkipBuild."
    }
}

$pgConnectionString = "Host=$pgFqdn;Database=$PostgresDb;Username=$PostgresUser;Password=$pgPass;SslMode=Require"

# 2. Harness team token.
$harnessToken = Get-ContainerAppSecret 'harness-team-token'
if (-not $harnessToken) {
    Write-Host "Registering 'kryptonian-collab' with the CA harness..."
    $resp = Invoke-RestMethod -Method POST -Uri "$HarnessBaseUrl/api/teams/register" -ContentType 'application/json' -Body '{"teamName":"kryptonian-collab"}'
    $harnessToken = $resp.token
}

# 3. Admin API key.
$adminApiKey = Get-ContainerAppSecret 'admin-api-key'
if (-not $adminApiKey) {
    $adminApiKey = New-RandomString -Bytes 32
    Write-Host "Generated new admin API key."
}

# 4. Build + push the image.
if (-not $SkipBuild) {
    Write-Host "Building image $Registry.azurecr.io/kryptonian-gateway:$ImageTag..."
    Push-Location $repoRoot
    try {
        $tags = @('--image', "kryptonian-gateway:$ImageTag")
        if ($ImageTag -ne 'latest') {
            $tags += @('--image', 'kryptonian-gateway:latest')
        }
        $acrArgs = @('acr', 'build', '--registry', $Registry) + $tags + @(
            '--build-arg', "VITE_API_KEY=$adminApiKey",
            '--file', 'src/RTSec.Kryptonian.Api/Dockerfile',
            '--no-logs',
            '.')
        $runId = az @acrArgs --query 'runId' -o tsv 2>$null
        if (-not $runId) { throw 'az acr build did not return a run id.' }
        Write-Host "  run id: $runId — polling..."
        do {
            Start-Sleep -Seconds 15
            $status = (az acr task show-run --registry $Registry --run-id $runId --query 'status' -o tsv 2>$null) -replace "`r|`n", ''
            Write-Host "    status: $status"
        } while ($status -in @('Running', 'Queued', 'Started'))
        if ($status -ne 'Succeeded') { throw "ACR run $runId ended with status '$status'." }
    } finally {
        Pop-Location
    }
}

# 5. Deploy the Bicep template.
Write-Host "Deploying main.bicep..."
$deploymentName = "kryptonian-gateway-$(Get-Date -Format 'yyyyMMddHHmmss')"
$deployArgs = @(
    'deployment', 'group', 'create',
    '-g', $ResourceGroup,
    '-n', $deploymentName,
    '--template-file', $bicepFile,
    '--parameters', "imageTag=$ImageTag",
    "adminApiKey=$adminApiKey",
    "harnessTeamToken=$harnessToken",
    "harnessBaseUrl=$HarnessBaseUrl",
    "postgresConnectionString=$pgConnectionString",
    '--query', 'properties.outputs'
)
$outputs = az @deployArgs -o json 2>&1
if ($LASTEXITCODE -ne 0) { throw "Bicep deployment failed: $outputs" }
$outputs = $outputs | ConvertFrom-Json
$gatewayUrl = $outputs.gatewayUrl.value

# 6. Add the pg-admin-password to the container app secrets too (so future
#    re-runs of this script can rediscover it without round-tripping to Postgres).
az containerapp secret set -g $ResourceGroup -n kryptonian-gateway --secrets "pg-admin-password=$pgPass" -o none

# 7. Seed CAs + EST profile.
if (-not $SkipSeed) {
    Write-Host "Seeding CA backends and EST profile..."
    & $seedScript -ResourceGroup $ResourceGroup -ContainerApp 'kryptonian-gateway' -GatewayUrl $gatewayUrl -AdminApiKey $adminApiKey -HarnessToken $harnessToken -HarnessBaseUrl $HarnessBaseUrl
}

Write-Host ''
Write-Host '=== Deployment complete ===' -ForegroundColor Green
Write-Host "Gateway URL:        $gatewayUrl"
Write-Host "Dashboard:          $gatewayUrl"
Write-Host "EST endpoint:       $gatewayUrl/.well-known/est/simpleenroll"
Write-Host "Health:             $gatewayUrl/api/status/health"
Write-Host ''
Write-Host "Postgres server:    $pgFqdn"
Write-Host '  retrieve admin password:'
Write-Host "  az containerapp secret show -g $ResourceGroup -n kryptonian-gateway --secret-name pg-admin-password --query value -o tsv"
Write-Host ''
Write-Host "Admin API key:      stored in ACA secret 'admin-api-key'"
Write-Host '  retrieve with:'
Write-Host "  az containerapp secret show -g $ResourceGroup -n kryptonian-gateway --secret-name admin-api-key --query value -o tsv"
Write-Host "Harness team token: stored in ACA secret 'harness-team-token'"
