#!/usr/bin/env pwsh
<#
.SYNOPSIS
    End-to-end activation smoke-test against the deployed gateway.

.DESCRIPTION
    Mimics what Kryptonian.MedicalDevice.exe does from the command line:
      1. POST /api/device-requests to register a pending device.
      2. Admin: POST /api/devices/{id}/approve.
      3. Admin: POST /api/devices/{id}/activation-code (returns an activation code).
      4. Build an RSA-4096 CSR with the device's CN.
      5. POST /.well-known/est/simpleenroll with X-Activation-Code header.
      6. Decode the PKCS#7 response, print the leaf cert's subject and issuer.

    Verifies that the gateway pre-seeded with the harness CAs can issue device
    certificates end-to-end. Pass -GatewayUrl to target a non-default deploy.

.EXAMPLE
    ./deploy/azure/smoke-test.ps1
#>

param(
    [string]$ResourceGroup = 'rg-kryptonian-hackathon',
    [string]$ContainerApp = 'kryptonian-gateway',
    [string]$GatewayUrl,
    [string]$AdminApiKey,
    [string]$CommonName = "smoke-test-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())"
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

if (-not $GatewayUrl) {
    $fqdn = az containerapp show -g $ResourceGroup -n $ContainerApp --query properties.configuration.ingress.fqdn -o tsv 2>$null
    $GatewayUrl = "https://$fqdn".Trim()
}
if (-not $AdminApiKey) {
    $AdminApiKey = (az containerapp secret show -g $ResourceGroup -n $ContainerApp --secret-name admin-api-key --query value -o tsv 2>$null).Trim()
}

$adminHeaders = @{ 'X-API-Key' = $AdminApiKey; 'Content-Type' = 'application/json' }

Write-Host "Gateway: $GatewayUrl"
Write-Host "Device CN: $CommonName"
Write-Host ""

# 1. Create the pending device via the anonymous /api/device-requests endpoint
Write-Host "[1/6] Registering pending device..."
$pendingBody = @{
    displayName       = $CommonName
    subjectCommonName = $CommonName
    manufacturer      = 'Smoke Test Co.'
    model             = 'Activation Probe'
    serialNumber      = 'SN-' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
} | ConvertTo-Json -Compress
$pending = Invoke-RestMethod -Method POST -Uri "$GatewayUrl/api/device-requests" -ContentType 'application/json' -Body $pendingBody
Write-Host "      device id: $($pending.deviceId)   status: $($pending.status)"

# 2. Generate an activation code via the admin API (consumed by /simpleenroll,
#    which atomically approves the device on first successful enrollment).
Write-Host "[2/6] Generating activation code..."
$codeResp = Invoke-RestMethod -Method POST -Uri "$GatewayUrl/api/devices/$($pending.deviceId)/activation-code" -Headers $adminHeaders -Body '{}'
$activationCode = $codeResp.activationCode
Write-Host "      activation code prefix: $($activationCode.Substring(0, 8))..."
Write-Host "      expires: $($codeResp.expiresAt)"

# 3. Build an RSA-4096 CSR with the device's CN
Write-Host "[3/5] Generating key pair and CSR..."
Add-Type -AssemblyName System.Security
$rsa = [System.Security.Cryptography.RSA]::Create(4096)
$subject = [System.Security.Cryptography.X509Certificates.X500DistinguishedName]::new("CN=$CommonName")
$req = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new(
    $subject,
    $rsa,
    [System.Security.Cryptography.HashAlgorithmName]::SHA256,
    [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
$csrDer = $req.CreateSigningRequest()
$csrB64 = [Convert]::ToBase64String($csrDer)

# 4. Enroll via EST
Write-Host "[4/5] POST /.well-known/est/simpleenroll..."
$enrollHeaders = @{
    'X-Activation-Code'         = $activationCode
    'X-Device-Manufacturer'     = 'Smoke Test Co.'
    'X-Device-Model'            = 'Activation Probe'
    'X-Device-Serial-Number'    = 'SN-' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
    'Content-Type'              = 'application/pkcs10'
    'Content-Transfer-Encoding' = 'base64'
}
$enrollResp = Invoke-WebRequest -Method POST -Uri "$GatewayUrl/.well-known/est/simpleenroll" -Headers $enrollHeaders -Body $csrB64 -SkipHttpErrorCheck
if ($enrollResp.StatusCode -ne 200) {
    Write-Host "      EST enrollment failed (HTTP $($enrollResp.StatusCode)):" -ForegroundColor Red
    Write-Host $enrollResp.Content
    exit 1
}

# 5. Decode the PKCS#7 response and print the leaf.
# The server returns a base64-encoded PKCS#7 envelope with Content-Type
# application/pkcs7-mime, which makes Invoke-WebRequest auto-deserialize the
# body as a byte[]. Convert it back to text first.
Write-Host "[5/5] Decoding issued certificate..."
if ($enrollResp.Content -is [byte[]]) {
    $b64Text = [System.Text.Encoding]::ASCII.GetString($enrollResp.Content).Trim()
} else {
    $b64Text = ([string]$enrollResp.Content).Trim()
}
$pkcs7 = [Convert]::FromBase64String($b64Text)
$cms = [System.Security.Cryptography.Pkcs.SignedCms]::new()
$cms.Decode($pkcs7)
$leaf = $cms.Certificates | Where-Object {
    $bc = $_.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.19' }
    -not $bc -or -not ([System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]$bc).CertificateAuthority
} | Select-Object -First 1
if (-not $leaf) { $leaf = $cms.Certificates[0] }

Write-Host ""
Write-Host "=== Issued certificate ===" -ForegroundColor Green
Write-Host "Subject:    $($leaf.Subject)"
Write-Host "Issuer:     $($leaf.Issuer)"
Write-Host "Serial:     $($leaf.SerialNumber)"
Write-Host "Thumbprint: $($leaf.Thumbprint)"
Write-Host "Not before: $($leaf.NotBefore)"
Write-Host "Not after:  $($leaf.NotAfter)"
$gatewayOidExt = $leaf.Extensions | Where-Object { $_.Oid.Value -like '1.3.6.1.4.1.99999.*' }
if ($gatewayOidExt) {
    Write-Host "Gateway OID: $($gatewayOidExt.Oid.Value) (issued by gateway via harness)" -ForegroundColor Cyan
}
Write-Host ""
Write-Host "End-to-end activation succeeded." -ForegroundColor Green
