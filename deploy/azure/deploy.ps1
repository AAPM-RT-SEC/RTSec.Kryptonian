#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Deploy the Kryptonian gateway to Azure end-to-end.

.DESCRIPTION
    Runs an idempotent sequence:
      1. (optional) az acr build — builds and pushes the image. Skip with -SkipBuild.
      2. Ensures a harness team token exists (registers `kryptonian-collab` if needed).
      3. Generates / loads an admin API key.
      4. Deploys deploy/azure/main.bicep into rg-kryptonian-hackathon.
      5. Runs deploy/azure/seed-gateway.ps1 to wire up the four CA backends + EST profile.

    All sensitive values flow as Bicep secure parameters or ACA secrets — they
    do not appear in plain text in the deployment history.

.PARAMETER ImageTag
.PARAMETER ResourceGroup
.PARAMETER SkipBuild     Skip ACR build; deploy whatever tag is already in ACR.
.PARAMETER SkipSeed      Skip the post-deploy seed step.

.EXAMPLE
    ./deploy/azure/deploy.ps1                 # full deploy
    ./deploy/azure/deploy.ps1 -SkipBuild      # redeploy without rebuilding
#>

param(
    [string]$ImageTag = 'v1',
    [string]$ResourceGroup = 'rg-kryptonian-hackathon',
    [string]$Registry = 'kryptonianregistry',
    [string]$HarnessBaseUrl = 'https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io',
    [switch]$SkipBuild,
    [switch]$SkipSeed
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..' '..')
$bicepFile = Join-Path $PSScriptRoot 'main.bicep'
$seedScript = Join-Path $PSScriptRoot 'seed-gateway.ps1'

function New-AdminApiKey {
    $bytes = [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32)
    return ([Convert]::ToBase64String($bytes) -replace '\+', '-' -replace '/', '_' -replace '=', '')
}

# 1. Find or create the harness team token. The harness has no "get my token"
#    endpoint, so we either reuse an existing ACA secret or register a fresh team.
$harnessToken = $null
$existing = az containerapp secret list -g $ResourceGroup -n kryptonian-gateway --query "[?name=='harness-team-token']" -o json 2>$null
if ($existing -and ($existing | ConvertFrom-Json).Count -gt 0) {
    $harnessToken = az containerapp secret show -g $ResourceGroup -n kryptonian-gateway --secret-name harness-team-token --query value -o tsv 2>$null
}
if (-not $harnessToken) {
    Write-Host "Registering 'kryptonian-collab' with the CA harness..."
    $body = '{"teamName":"kryptonian-collab"}'
    $resp = Invoke-RestMethod -Method POST -Uri "$HarnessBaseUrl/api/teams/register" -ContentType 'application/json' -Body $body
    $harnessToken = $resp.token
    Write-Host "  team:  $($resp.teamName)"
    Write-Host "  token: $($harnessToken.Substring(0, 8))..."
}

# 2. Admin API key — reuse the deployed secret if it exists, otherwise mint one.
$adminApiKey = $null
$existingKey = az containerapp secret list -g $ResourceGroup -n kryptonian-gateway --query "[?name=='admin-api-key']" -o json 2>$null
if ($existingKey -and ($existingKey | ConvertFrom-Json).Count -gt 0) {
    $adminApiKey = az containerapp secret show -g $ResourceGroup -n kryptonian-gateway --secret-name admin-api-key --query value -o tsv 2>$null
}
if (-not $adminApiKey) {
    Write-Host "Generating new admin API key..."
    $adminApiKey = New-AdminApiKey
}

# 3. Build + push the image. The same admin key is baked into the UI bundle so
#    the dashboard authenticates with the same X-API-Key the backend accepts.
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
        if (-not $runId) {
            throw "az acr build did not return a run id."
        }
        Write-Host "  run id: $runId — polling..."
        do {
            Start-Sleep -Seconds 15
            $status = (az acr task show-run --registry $Registry --run-id $runId --query 'status' -o tsv 2>$null) -replace "`r|`n", ''
            Write-Host "    status: $status"
        } while ($status -in @('Running', 'Queued', 'Started'))
        if ($status -ne 'Succeeded') {
            throw "ACR run $runId ended with status '$status'."
        }
    } finally {
        Pop-Location
    }
}

# 4. Deploy the Bicep template.
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
    '--query', 'properties.outputs'
)
$outputs = az @deployArgs -o json 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "Bicep deployment failed: $outputs"
}
$outputs = $outputs | ConvertFrom-Json
$gatewayUrl = $outputs.gatewayUrl.value
$gatewayFqdn = $outputs.gatewayFqdn.value
Write-Host "  gateway URL: $gatewayUrl"

# 5. Seed CAs + EST profile.
if (-not $SkipSeed) {
    Write-Host "Seeding CA backends and EST profile..."
    & $seedScript -ResourceGroup $ResourceGroup -ContainerApp 'kryptonian-gateway' -GatewayUrl $gatewayUrl -AdminApiKey $adminApiKey -HarnessToken $harnessToken -HarnessBaseUrl $HarnessBaseUrl
}

Write-Host ""
Write-Host "=== Deployment complete ===" -ForegroundColor Green
Write-Host "Gateway URL:       $gatewayUrl"
Write-Host "Dashboard:         $gatewayUrl"
Write-Host "EST endpoint:      $gatewayUrl/.well-known/est/simpleenroll"
Write-Host "Health:            $gatewayUrl/api/status/health"
Write-Host ""
Write-Host "Admin API key:     stored in ACA secret 'admin-api-key'"
Write-Host "  retrieve with:   az containerapp secret show -g $ResourceGroup -n kryptonian-gateway --secret-name admin-api-key --query value -o tsv"
Write-Host "Harness team token: stored in ACA secret 'harness-team-token'"
