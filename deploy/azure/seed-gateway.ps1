#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Seed the deployed Kryptonian gateway with the four CA backends and one EST profile.

.DESCRIPTION
    Idempotent. Reads the gateway FQDN, admin API key, and harness team token,
    then POSTs to /api/cas (×4), activates the selfsigned backend, and POSTs to
    /api/est-profiles (×1) pointing at the active backend.

    Existing backends with the same name are left alone (the script logs and
    skips). Missing values are pulled from the deployed container app via
    `az containerapp` so re-runs need no extra arguments once the deployment
    has happened.

.PARAMETER ResourceGroup
.PARAMETER ContainerApp
.PARAMETER HarnessBaseUrl
.PARAMETER GatewayUrl     Override gateway URL (defaults to the container app's FQDN).
.PARAMETER AdminApiKey    Override admin API key (defaults to az containerapp secret show).
.PARAMETER HarnessToken   Override harness team token (defaults to az containerapp secret show).

.EXAMPLE
    ./deploy/azure/seed-gateway.ps1
#>

param(
    [string]$ResourceGroup = 'rg-kryptonian-hackathon',
    [string]$ContainerApp = 'kryptonian-gateway',
    [string]$HarnessBaseUrl = 'https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io',
    [string]$GatewayUrl,
    [string]$AdminApiKey,
    [string]$HarnessToken
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

function Get-Secret {
    param([string]$Name)
    $value = az containerapp secret show -g $ResourceGroup -n $ContainerApp --secret-name $Name --query value -o tsv 2>$null
    if (-not $value) {
        throw "Could not read secret '$Name' from container app '$ContainerApp'. Has it been deployed?"
    }
    return ([string]$value).Trim()
}

if (-not $GatewayUrl) {
    $fqdn = az containerapp show -g $ResourceGroup -n $ContainerApp --query properties.configuration.ingress.fqdn -o tsv 2>$null
    if (-not $fqdn) {
        throw "Could not resolve FQDN for container app '$ContainerApp' in '$ResourceGroup'."
    }
    $GatewayUrl = "https://$fqdn".Trim()
}

if (-not $AdminApiKey) { $AdminApiKey = Get-Secret 'admin-api-key' }
if (-not $HarnessToken) { $HarnessToken = Get-Secret 'harness-team-token' }

Write-Host "Gateway:        $GatewayUrl"
Write-Host "Harness:        $HarnessBaseUrl"
Write-Host "Team token:     $($HarnessToken.Substring(0, 8))..."

$harnessTeamUrl = "$HarnessBaseUrl/teams/$HarnessToken"

# The four CA backends. selfsigned is marked active.
$caSeeds = @(
    [pscustomobject]@{
        name = 'Harness Self-Signed (EST)'
        type = 'selfsigned'
        url = $harnessTeamUrl
        config = @{ HarnessBaseUrl = $harnessTeamUrl }
        isEnabled = $true
        isActive = $true
    }
    [pscustomobject]@{
        name = 'Harness ADCS (SCEP)'
        type = 'adcs'
        url = $harnessTeamUrl
        config = @{
            HarnessBaseUrl = $harnessTeamUrl
            TemplateName = 'DicomDeviceAuthentication'
            ValidityDays = 7
        }
        isEnabled = $true
        isActive = $false
    }
    [pscustomobject]@{
        name = 'Harness EJBCA (REST)'
        type = 'ejbca'
        url = $harnessTeamUrl
        config = @{
            HarnessBaseUrl = $harnessTeamUrl
            CertificateProfile = 'MedicalDeviceTLS'
            EndEntityProfile = 'DicomDevice'
            ValidityDays = 7
        }
        isEnabled = $true
        isActive = $false
    }
    [pscustomobject]@{
        name = 'Harness ACME (step-ca)'
        type = 'acme'
        url = "$harnessTeamUrl/acme/directory"
        config = @{
            DirectoryUrl = "$harnessTeamUrl/acme/directory"
            Email = 'admin@example.com'
            PreferredChallengeType = 'http-01'
        }
        isEnabled = $true
        isActive = $false
    }
)

$headers = @{
    'X-API-Key'    = $AdminApiKey
    'Content-Type' = 'application/json'
}

function Invoke-Gateway {
    param([string]$Method, [string]$Path, $Body = $null)
    $uri = "$GatewayUrl$Path"
    $args = @{ Method = $Method; Uri = $uri; Headers = $headers; SkipHttpErrorCheck = $true }
    if ($null -ne $Body) { $args.Body = ($Body | ConvertTo-Json -Depth 8 -Compress) }
    return Invoke-WebRequest @args
}

# Wait until the gateway is responsive.
$deadline = (Get-Date).AddMinutes(5)
do {
    try {
        $r = Invoke-WebRequest -Uri "$GatewayUrl/api/status/health" -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
        if ($r.StatusCode -eq 200) { break }
    } catch {
        Start-Sleep -Seconds 5
    }
} while ((Get-Date) -lt $deadline)

# Pull existing backends so we skip ones already present (idempotency).
$existingBackends = (Invoke-Gateway -Method GET -Path '/api/cas').Content | ConvertFrom-Json
$existingNames = @($existingBackends | ForEach-Object { $_.name })

foreach ($seed in $caSeeds) {
    if ($existingNames -contains $seed.name) {
        Write-Host "skip  CA  '$($seed.name)' (already exists)"
        continue
    }
    $resp = Invoke-Gateway -Method POST -Path '/api/cas' -Body $seed
    if ($resp.StatusCode -ge 400) {
        throw "Failed to create CA backend '$($seed.name)' (HTTP $($resp.StatusCode)): $($resp.Content)"
    }
    Write-Host "+ CA   '$($seed.name)' [$($seed.type)]"
}

# Activate the selfsigned backend if not already active.
$backends = (Invoke-Gateway -Method GET -Path '/api/cas').Content | ConvertFrom-Json
$activeSeed = $backends | Where-Object { $_.type -eq 'selfsigned' } | Select-Object -First 1
if ($activeSeed -and -not $activeSeed.isActive) {
    Invoke-Gateway -Method POST -Path "/api/cas/$($activeSeed.id)/activate" | Out-Null
    Write-Host "activated CA '$($activeSeed.name)'"
}

# Seed a single EST profile. PathPrefix /.well-known/est is the EST default;
# hostname matching is in-process so the hostname list is informational here.
$gatewayHost = ([Uri]$GatewayUrl).Host
$profileSeed = [pscustomobject]@{
    name = 'Default Device EST'
    hostnames = @($gatewayHost)
    hostnameMatchType = 'exact'
    pathPrefix = '/.well-known/est'
    caBackendId = $activeSeed.id
    allowedKeyUsages = @('digitalSignature', 'keyEncipherment')
    validityDays = 7
    requireClientCertificate = $false
    validateClientCertificateChain = $false
    trustedClientCaThumbprints = @()
    isEnabled = $true
}

$existingProfiles = (Invoke-Gateway -Method GET -Path '/api/est-profiles').Content | ConvertFrom-Json
if (@($existingProfiles).Where{ $_.name -eq $profileSeed.name }) {
    Write-Host "skip  EST profile '$($profileSeed.name)' (already exists)"
} else {
    $resp = Invoke-Gateway -Method POST -Path '/api/est-profiles' -Body $profileSeed
    if ($resp.StatusCode -ge 400) {
        throw "Failed to create EST profile (HTTP $($resp.StatusCode)): $($resp.Content)"
    }
    Write-Host "+ EST profile '$($profileSeed.name)' -> $($activeSeed.name)"
}

Write-Host ""
Write-Host "Seed complete. Dashboard: $GatewayUrl" -ForegroundColor Green
