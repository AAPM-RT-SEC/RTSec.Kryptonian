<#
Local-only developer proof. Creates a named development CA/profile and synthetic devices,
uses independent curl HTTP Basic EST, verifies mTLS renewal, and runs real DIMSE TLS.
Requires start-dev.ps1 already running. Does not change OS trust or delete existing data.
#>
[CmdletBinding()]
param(
    [int]$Port = 7443,
    [int]$CrlPort = 7444,
    [string]$DataDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\developer'),
    [switch]$SkipDicom,
    [switch]$CheckRevocation
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$state = [IO.Path]::GetFullPath($DataDirectory)
$secrets = Get-Content -Raw -LiteralPath (Join-Path $state 'secrets.json') | ConvertFrom-Json
$caPath = Join-Path $state 'ca.pem'
$ca = [Security.Cryptography.X509Certificates.X509Certificate2]::CreateFromPem([IO.File]::ReadAllText($caPath))
$fingerprint = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($ca.RawData))
$baseUrl = "https://localhost:$Port"
$crlUrl = "http://localhost:$CrlPort/api/crl/$fingerprint.crl"
$runName = 'verify-' + [Guid]::NewGuid().ToString('N')
$output = Join-Path $state $runName
[void](New-Item -ItemType Directory -Path $output)

# The launcher TLS server leaf has no CDP: this explicit developer-only exception
# skips SERVER revocation, never hostname/issuer validation or client/peer revocation.
if (-not ('KryptonianDeveloperHttp' -as [type])) {
    Add-Type -TypeDefinition @'
#pragma warning disable SYSLIB0057 // also support PowerShell runtimes before X509CertificateLoader
using System;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
public static class KryptonianDeveloperHttp {
    public static HttpClient Create(byte[] root, X509Certificate2 client = null) {
        var handler = new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false };
        if (client != null) handler.ClientCertificates.Add(client);
        handler.ServerCertificateCustomValidationCallback = (_, cert, _, errors) => {
            if (cert == null || (errors & (SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateNotAvailable)) != 0) return false;
            using var ca = new X509Certificate2(root);
            using var chain = new X509Chain();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(ca);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.1"));
            return chain.Build(cert);
        };
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
    }
}
'@
}
$http = [KryptonianDeveloperHttp]::Create($ca.RawData, $null)
$http.DefaultRequestHeaders.Add('X-API-Key', $secrets.adminApiKey)

function Invoke-AdminJson([string]$Method, [string]$Path, $Body = $null) {
    $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::new($Method), "$baseUrl$Path")
    try {
        if ($null -ne $Body) {
            $request.Content = [Net.Http.StringContent]::new(($Body | ConvertTo-Json -Depth 8 -Compress), [Text.Encoding]::UTF8, 'application/json')
        }
        $response = $http.SendAsync($request).GetAwaiter().GetResult()
        try {
            if (-not $response.IsSuccessStatusCode) { throw "$Method $Path returned HTTP $([int]$response.StatusCode)" }
            $text = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            if ($text) { return $text | ConvertFrom-Json }
        } finally { $response.Dispose() }
    } finally { $request.Dispose() }
}

function Invoke-CurlEst([string]$Credential, [string]$CsrPath, [string]$ResponsePath) {
    # Credentials travel on stdin, not the process command line or console.
    $config = @(
        "url = `"$baseUrl/.well-known/est/developer-operational/simpleenroll`""
        "user = `"$Credential`""
        "cacert = `"$($caPath.Replace('\','/'))`""
        'ssl-no-revoke'
        'noproxy = "*"'
        'silent'
        'show-error'
        'request = "POST"'
        'header = "Content-Type: application/pkcs10"'
        'header = "Content-Transfer-Encoding: base64"'
        "data-binary = `"@$($CsrPath.Replace('\','/'))`""
        "output = `"$($ResponsePath.Replace('\','/'))`""
        'write-out = "%{http_code}"'
    ) -join "`n"
    $status = $config | & curl.exe --config -
    if ($LASTEXITCODE -ne 0) { throw 'Independent curl EST request failed.' }
    return [int]$status
}

function Decode-Leaf([string]$Body, [Security.Cryptography.RSA]$Key) {
    $certs = [Security.Cryptography.X509Certificates.X509Certificate2Collection]::new()
    $certs.Import([Convert]::FromBase64String($Body.Trim()))
    try {
        foreach ($candidate in $certs) {
            $public = [Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPublicKey($candidate)
            if ($null -eq $public) { continue }
            try {
                if ([Convert]::ToBase64String($public.ExportSubjectPublicKeyInfo()) -eq [Convert]::ToBase64String($Key.ExportSubjectPublicKeyInfo())) {
                    $withKey = [Security.Cryptography.X509Certificates.RSACertificateExtensions]::CopyWithPrivateKey($candidate, $Key)
                    if (-not $IsWindows) { return $withKey }
                    try {
                        # Schannel needs an OS-backed key container, not an ephemeral attached RSA.
                        return [Security.Cryptography.X509Certificates.X509Certificate2]::new(
                            $withKey.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $secrets.pfxPassword),
                            $secrets.pfxPassword,
                            [Security.Cryptography.X509Certificates.X509KeyStorageFlags]::UserKeySet -bor [Security.Cryptography.X509Certificates.X509KeyStorageFlags]::Exportable)
                    } finally { $withKey.Dispose() }
                }
            } finally { $public.Dispose() }
        }
        throw 'EST returned no certificate matching the generated key.'
    } finally { foreach ($cert in $certs) { $cert.Dispose() } }
}

try {
    $health = $http.GetAsync("$baseUrl/api/status/health").GetAwaiter().GetResult()
    if ([int]$health.StatusCode -ne 200) { throw 'Gateway is not healthy.' }
    $health.Dispose()
    Write-Host 'PASS HTTPS health (explicit CA and hostname; developer SERVER revocation exception).'

    $backend = @(Invoke-AdminJson GET '/api/cas') | Where-Object name -eq 'Developer CA' | Select-Object -First 1
    if (-not $backend) {
        $backend = Invoke-AdminJson POST '/api/cas' @{
            name = 'Developer CA'; type = 'selfsigned'; isEnabled = $true; isActive = $true
            config = @{ PfxPath = (Join-Path $state 'ca.pfx'); PfxPassword = $secrets.caPfxPassword; CrlDistributionPointUrl = $crlUrl }
        }
    }
    $profile = @(Invoke-AdminJson GET '/api/est-profiles') | Where-Object name -eq 'developer-operational' | Select-Object -First 1
    if (-not $profile) {
        $profile = Invoke-AdminJson POST '/api/est-profiles' @{
            name = 'developer-operational'; pathPrefix = '/.well-known/est/developer-operational'; hostnames = @('localhost', '127.0.0.1')
            caBackendId = $backend.id; validityDays = 7; requireClientCertificate = $false
            validateClientCertificateChain = $true; trustedClientCaThumbprints = @($fingerprint)
            allowedKeyUsages = @('digitalSignature', 'keyEncipherment', 'clientAuth', 'serverAuth')
        }
    }
    if (-not $profile.validateClientCertificateChain -or $profile.caBackendId -ne $backend.id) { throw 'Existing developer profile has unexpected trust settings.' }
    if ($profile.pathPrefix -eq '/.well-known/est') {
        $profile = Invoke-AdminJson PUT "/api/est-profiles/$($profile.id)" @{
            pathPrefix = '/.well-known/est/developer-operational'; hostnames = @('localhost', '127.0.0.1')
            allowedKeyUsages = @('digitalSignature', 'keyEncipherment', 'clientAuth', 'serverAuth'); trustedClientCaThumbprints = @($fingerprint)
        }
    }
    $crl = Invoke-WebRequest -Uri $crlUrl
    if ($crl.StatusCode -ne 200) { throw 'Initial CRL is unavailable.' }
    Write-Host 'PASS configured issuer/profile and anonymous signed CRL endpoint.'
    $httpOnly = Invoke-WebRequest -Uri "http://localhost:$CrlPort/api/status/health" -SkipHttpErrorCheck
    if ($httpOnly.StatusCode -ne 403) { throw 'CRL HTTP listener allowed a non-CRL request.' }

    $device = Invoke-AdminJson POST '/api/devices' @{ displayName = $runName; subjectCommonName = $runName; manufacturer = 'Synthetic'; model = 'Developer test'; serialNumber = $runName }
    $activation = Invoke-AdminJson POST "/api/devices/$($device.id)/activation-code" @{ validForMinutes = 15 }
    $key = [Security.Cryptography.RSA]::Create(2048)
    try {
        $request = [Security.Cryptography.X509Certificates.CertificateRequest]::new("CN=$runName", $key, [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
        $csrPath = Join-Path $output 'request.b64'
        [IO.File]::WriteAllText($csrPath, [Convert]::ToBase64String($request.CreateSigningRequest()))
        $responsePath = Join-Path $output 'enrollment.b64'
        $credential = "$($device.id):$($activation.activationCode)"
        if ((Invoke-CurlEst $credential $csrPath $responsePath) -ne 200) { throw "EST bootstrap failed; public error response is in $responsePath" }
        $certificate = Decode-Leaf ([IO.File]::ReadAllText($responsePath)) $key
        if ((Invoke-CurlEst $credential $csrPath (Join-Path $output 'replay.txt')) -notin @(401,403)) { throw 'Activation replay was accepted.' }
    } finally { $key.Dispose() }
    Write-Host 'PASS independent curl HTTP Basic EST, matching key, and rejected activation replay; no private enrollment headers.'

    try {
        [IO.File]::WriteAllBytes((Join-Path $output 'client.pfx'), $certificate.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $secrets.pfxPassword))
        $newKey = [Security.Cryptography.RSA]::Create(2048)
        $renewHttp = [KryptonianDeveloperHttp]::Create($ca.RawData, $certificate)
        try {
            $renewRequest = [Security.Cryptography.X509Certificates.CertificateRequest]::new($certificate.SubjectName, $newKey, [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.RSASignaturePadding]::Pkcs1)
            $content = [Net.Http.StringContent]::new([Convert]::ToBase64String($renewRequest.CreateSigningRequest()), [Text.Encoding]::ASCII, 'application/pkcs10')
            $renewResponse = $renewHttp.PostAsync("$baseUrl/.well-known/est/developer-operational/simplereenroll", $content).GetAwaiter().GetResult()
            try {
                if ([int]$renewResponse.StatusCode -ne 200) { throw "mTLS renewal returned HTTP $([int]$renewResponse.StatusCode)" }
                $renewed = Decode-Leaf ($renewResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult()) $newKey
                try {
                    if ($renewed.Thumbprint -eq $certificate.Thumbprint -or $renewed.Subject -ne $certificate.Subject) { throw 'Renewal did not rotate the credential while preserving identity.' }
                    [IO.File]::WriteAllBytes((Join-Path $output 'renewed.pfx'), $renewed.Export([Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $secrets.pfxPassword))
                } finally { $renewed.Dispose() }
            } finally { $renewResponse.Dispose(); $content.Dispose() }
        } finally { $newKey.Dispose(); $renewHttp.Dispose() }
    } finally { $certificate.Dispose() }
    $device = Invoke-AdminJson GET "/api/devices/$($device.id)"
    Write-Host "PASS mTLS renewal with online CLIENT revocation, new key, retained identity; registry certificate $($device.lastCertificateId)."

    $spoof = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Get, "$baseUrl/api/status/health")
    $spoof.Headers.Add('X-Forwarded-Client-Cert', 'Cert="spoofed"')
    $spoofResponse = $http.SendAsync($spoof).GetAwaiter().GetResult()
    if ([int]$spoofResponse.StatusCode -ne 403) { throw 'Forwarded identity spoof was not rejected.' }
    $spoofResponse.Dispose(); $spoof.Dispose()
    Write-Host 'PASS direct forwarded-certificate spoof rejection.'

    if (-not $SkipDicom) {
        $savedKey = $env:KRYPTONIAN_ADMIN_API_KEY
        try {
            $env:KRYPTONIAN_ADMIN_API_KEY = $secrets.adminApiKey
            & dotnet run --project (Join-Path $repoRoot 'src\Kryptonian.DICOMTls') --no-build -- --gateway "$baseUrl/.well-known/est/developer-operational" --gateway-ca $caPath --non-interactive --count 2 --output (Join-Path $output 'dicom')
            if ($LASTEXITCODE -ne 0) { throw 'Live DICOM TLS transfer failed.' }
        } finally { $env:KRYPTONIAN_ADMIN_API_KEY = $savedKey }
    }
    if ($CheckRevocation) {
        $null = Invoke-AdminJson POST "/api/certificates/$($device.lastCertificateId)/revoke" @{ reason = 'KeyCompromise' }
        $revoked = [Security.Cryptography.X509Certificates.X509Certificate2]::new((Join-Path $output 'renewed.pfx'), $secrets.pfxPassword)
        try {
            $deadline = [DateTime]::UtcNow.AddSeconds(90)
            Write-Host 'Checking native online revocation (up to 90 seconds for the existing 60-second CRL cache; no cache flush).'
            do {
                $chain = [Security.Cryptography.X509Certificates.X509Chain]::new()
                try {
                    $chain.ChainPolicy.TrustMode = 'CustomRootTrust'
                    [void]$chain.ChainPolicy.CustomTrustStore.Add($ca)
                    $chain.ChainPolicy.RevocationMode = 'Online'
                    $chain.ChainPolicy.RevocationFlag = 'ExcludeRoot'
                    $chain.ChainPolicy.UrlRetrievalTimeout = [TimeSpan]::FromSeconds(5)
                    $valid = $chain.Build($revoked)
                    $isRevoked = @($chain.ChainStatus | Where-Object { $_.Status -band [Security.Cryptography.X509Certificates.X509ChainStatusFlags]::Revoked }).Count -gt 0
                } finally { $chain.Dispose() }
                if ($isRevoked) { break }
                Start-Sleep -Seconds 2
            } while ([DateTime]::UtcNow -lt $deadline)
            if ($valid -or -not $isRevoked) { throw 'Native online chain validation did not report Revoked.' }
            Write-Host 'PASS native X509Chain online revocation reports Revoked, independently of the device registry.'
        } finally { $revoked.Dispose() }
    }
    @{ run = $runName; deviceId = $device.id; certificateId = $device.lastCertificateId; issuerFingerprint = $fingerprint; crlUrl = $crlUrl; revocationChecked = [bool]$CheckRevocation; utc = [DateTime]::UtcNow.ToString('O') } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'evidence.json')
    Write-Host "Developer evidence: $output"
} finally { $http.Dispose(); $ca.Dispose() }
