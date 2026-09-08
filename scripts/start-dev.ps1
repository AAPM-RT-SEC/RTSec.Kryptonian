<#
.SYNOPSIS
    Start the Kryptonian API locally over HTTPS with a durable SQLite database.

.DESCRIPTION
    Generates local developer PKI and state, builds the UI and API, sets
    process-scoped environment variables, then runs the API in the foreground.

    Everything generated lives under artifacts\developer (git-ignored):
      ca.pfx / ca.pem       local development CA (keyCertSign + cRLSign)
      server.pfx            TLS server cert, SAN localhost + 127.0.0.1, serverAuth
      kryptonian.db         EF Core SQLite database (created by the API on first run)
      secrets.json          PFX passwords, JWT key, admin API key (current-user ACL)

    This script makes no API calls, writes no database rows, seeds no accounts or
    passwords, and never touches OS certificate trust stores. It binds only to
    loopback. After it starts, complete the admin setup wizard in the dashboard and
    configure a CA backend plus an EST profile before enrolling any device.

    Development convenience only: not security reviewed, not production or
    clinically safe, and it does not bootstrap device trust automatically.

.PARAMETER Port
    Loopback HTTPS port. Default 7443.

.PARAMETER DataDirectory
    Override the state/cert directory. Default artifacts\developer under the repo root.

.PARAMETER NoBuild
    Skip UI and API build steps and run from existing output.

.EXAMPLE
    .\scripts\start-dev.ps1
    .\scripts\start-dev.ps1 -Port 7543 -NoBuild
    .\scripts\start-dev.ps1 -DataDirectory D:\kryp-state
#>

[CmdletBinding()]
param(
    [ValidateRange(1024, 65535)]
    [int]$Port = 7443,

    [ValidateRange(1024, 65535)]
    [int]$CrlPort = 7444,

    [string]$DataDirectory,

    [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# PowerShell 7 on Windows only: the ACL and certificate paths below rely on it.
if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw "PowerShell 7 or later is required (found $($PSVersionTable.PSVersion)). Run this script with pwsh."
}
if (-not $IsWindows) {
    throw 'This launcher is Windows-only; use docker compose or scripts/generate-test-certs.sh elsewhere.'
}

# Resolve to an absolute path so relative -DataDirectory values cannot shift between
# the repo root, the UI project, and the API working directory.
$RepoRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if ($DataDirectory) {
    $StateDir = [System.IO.Path]::GetFullPath(
        $DataDirectory, [Environment]::CurrentDirectory)
} else {
    $StateDir = [System.IO.Path]::GetFullPath((Join-Path $RepoRoot 'artifacts\developer'))
}

$CaPfxPath   = Join-Path $StateDir 'ca.pfx'
$CaPemPath   = Join-Path $StateDir 'ca.pem'
$ServerPfx   = Join-Path $StateDir 'server.pfx'
$DbPath      = Join-Path $StateDir 'kryptonian.db'
$SecretsPath = Join-Path $StateDir 'secrets.json'

$BaseUrl    = "https://localhost:$Port"
if ($Port -eq $CrlPort) { throw 'HTTPS and CRL ports must differ.' }
$ApiProject = Join-Path $RepoRoot 'src\RTSec.Kryptonian.Api'
$UiProject  = Join-Path $RepoRoot 'src\RTSec.Kryptonian.Ui'

# Loaded once per run so the CA password can reach both the PFX files and the
# KRYPTONIAN__CA__SELFSIGNED__* variables the API reads.
$script:DevSecrets = $null

function Write-Step([string]$Message) { Write-Host "[dev] $Message" -ForegroundColor Cyan }
function Write-Caution([string]$Message) { Write-Host "[dev] $Message" -ForegroundColor Yellow }

function New-RandomSecret {
    $bytes = [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48)
    return [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

# Secrets are created once and reused across runs. Nothing here deletes them or the database.
# The file is created EMPTY and locked down BEFORE any key material is written, and ACL
# failures are fatal rather than a warning, so secrets can never land in a loose DACL.
function Get-OrCreateSecrets {
    if (Test-Path $SecretsPath) {
        return Get-Content -Raw -Path $SecretsPath | ConvertFrom-Json
    }

    $secrets = [pscustomobject]@{
        caPfxPassword = New-RandomSecret
        pfxPassword   = New-RandomSecret
        jwtSecret     = New-RandomSecret
        adminApiKey   = New-RandomSecret
    }

    # Create the empty placeholder first; no secret bytes yet.
    Set-Content -Path $SecretsPath -Value '' -Encoding utf8NoBOM -NoNewline

    $sid = (New-Object System.Security.Principal.SecurityIdentifier(
        [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value)).Translate(
        [System.Security.Principal.NTAccount]).Value

    $acl = Get-Acl -Path $SecretsPath
    $acl.SetAccessRuleProtection($true, $false)          # drop inherited access
    foreach ($rule in @($acl.Access)) { [void]$acl.RemoveAccessRuleSpecific($rule) }
    foreach ($principal in @($sid, 'NT AUTHORITY\SYSTEM')) {
        $acl.AddAccessRule([System.Security.AccessControl.FileSystemAccessRule]::new(
            $principal, 'FullControl', 'None', 'None', 'Allow'))
    }
    Set-Acl -Path $SecretsPath -AclObject $acl

    # Fail closed: re-read the DACL and require exactly the two intended principals.
    # Compare-Object emits difference objects when the sets differ and NOTHING when they
    # match, so a non-empty result is the failure case. (Do not negate this test: an
    # empty result means the ACL is correct.)
    $allowed = @((Get-Acl -Path $SecretsPath).Access |
        ForEach-Object { $_.IdentityReference.Translate([System.Security.Principal.NTAccount]).Value }) |
        Sort-Object -Unique
    $expected = @($sid, 'NT AUTHORITY\SYSTEM') | Sort-Object -Unique
    if (Compare-Object $allowed $expected -SyncWindow 0) {
        throw "Could not restrict ACL on $SecretsPath (found: $($allowed -join ', ')). Refusing to write secrets."
    }

    Set-Content -Path $SecretsPath -Value ($secrets | ConvertTo-Json) -Encoding utf8NoBOM -NoNewline
    return $secrets
}

# --- Certificates via .NET APIs only: no OpenSSL, no trust-store mutation. ---
function New-DeveloperCa {
    $key = [System.Security.Cryptography.RSA]::Create(4096)
    $cert = $null
    try {
        $subject = New-Object System.Security.Cryptography.X509Certificates.X500DistinguishedName 'CN=Kryptonian Dev CA'
        $req = New-Object System.Security.Cryptography.X509Certificates.CertificateRequest(
            $subject, $key,
            [System.Security.Cryptography.HashAlgorithmName]::SHA256,
            [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)

        $basic = New-Object System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension($true, $false, 0, $true)
        # Issuer only: keyCertSign + cRLSign. Deliberately no digitalSignature/keyEncipherment.
        $usage = New-Object System.Security.Cryptography.X509Certificates.X509KeyUsageExtension(
            ([System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::KeyCertSign -bor
             [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::CrlSign), $true)
        $ski = New-Object System.Security.Cryptography.X509Certificates.X509SubjectKeyIdentifierExtension($req.PublicKey, $false)

        $req.CertificateExtensions.Add($basic)
        $req.CertificateExtensions.Add($usage)
        $req.CertificateExtensions.Add($ski)

        $cert = $req.CreateSelfSigned([DateTimeOffset]::UtcNow.AddDays(-1), [DateTimeOffset]::UtcNow.AddYears(5))
        $pfx = $cert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $script:DevSecrets.caPfxPassword)
        Set-Content -Path $CaPfxPath -Value $pfx -AsByteStream
        [System.IO.File]::WriteAllText($CaPemPath, $cert.ExportCertificatePem())
        Write-Step "Created development CA: $CaPfxPath"
    } finally {
        if ($cert) { $cert.Dispose() }
        $key.Dispose()
    }
}

function New-DeveloperServerCert {
    $ca = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2(
        $CaPfxPath, $script:DevSecrets.caPfxPassword,
        [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::Exportable)
    $key = [System.Security.Cryptography.RSA]::Create(2048)
    $issued = $null
    $withKey = $null
    try {
        $subject = New-Object System.Security.Cryptography.X509Certificates.X500DistinguishedName 'CN=localhost'
        $req = New-Object System.Security.Cryptography.X509Certificates.CertificateRequest(
            $subject, $key,
            [System.Security.Cryptography.HashAlgorithmName]::SHA256,
            [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)

        $basic = New-Object System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension($false, $false, 0, $true)
        $usage = New-Object System.Security.Cryptography.X509Certificates.X509KeyUsageExtension(
            ([System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature -bor
             [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::KeyEncipherment), $true)

        $ekus = New-Object System.Security.Cryptography.OidCollection
        [void]$ekus.Add((New-Object System.Security.Cryptography.Oid('1.3.6.1.5.5.7.3.1')))  # serverAuth
        $eku = New-Object System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension($ekus, $false)

        $sanBuilder = New-Object System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder
        $sanBuilder.AddDnsName('localhost')
        $sanBuilder.AddIpAddress([System.Net.IPAddress]::Loopback)

        foreach ($ext in @($basic, $usage, $eku, $sanBuilder.Build())) { $req.CertificateExtensions.Add($ext) }

        $serial = New-Object byte[] 16
        [System.Security.Cryptography.RandomNumberGenerator]::Fill($serial)
        $serial[0] = $serial[0] -band 0x7F

        $issued = $req.Create($ca, [DateTimeOffset]::UtcNow.AddDays(-1), [DateTimeOffset]::UtcNow.AddYears(2), $serial)
        # Static form: the extension method is not guaranteed on the instance in all hosts.
        $withKey = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::CopyWithPrivateKey($issued, $key)
        $pfx = $withKey.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $script:DevSecrets.pfxPassword)
        Set-Content -Path $ServerPfx -Value $pfx -AsByteStream
        Write-Step "Created loopback server certificate: $ServerPfx"
    } finally {
        if ($withKey) { $withKey.Dispose() }
        if ($issued) { $issued.Dispose() }
        $key.Dispose()
        $ca.Dispose()
    }
}

$savedEnv = @{}
try {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'dotnet SDK is not installed or not on PATH.'
    }

    New-Item -ItemType Directory -Force -Path $StateDir | Out-Null
    $script:DevSecrets = Get-OrCreateSecrets

    if (-not (Test-Path $CaPfxPath)) { New-DeveloperCa }
    if (-not (Test-Path $ServerPfx)) { New-DeveloperServerCert }

    if (-not $NoBuild) {
        if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
            throw 'npm is required to build the dashboard. Install Node.js, or pass -NoBuild to reuse existing UI output.'
        }

        Push-Location $UiProject
        try {
            Write-Step 'Building React UI...'
            # Strict: a failed clean install must not be papered over with npm install.
            & npm ci --no-audit --no-fund
            if ($LASTEXITCODE -ne 0) { throw "npm ci failed (exit $LASTEXITCODE). Fix package-lock.json rather than falling back." }
            & npm run build
            if ($LASTEXITCODE -ne 0) { throw 'UI build failed.' }
        } finally { Pop-Location }

        Write-Step 'Building API...'
        & dotnet build $ApiProject --nologo -v quiet
        if ($LASTEXITCODE -ne 0) { throw 'API build failed.' }
    }

    # Process-scoped only; restored in the outer finally so the caller shell is untouched.
    $envMap = [ordered]@{
        'ASPNETCORE_ENVIRONMENT'                  = 'Development'
        'ASPNETCORE_URLS'                         = "$BaseUrl;http://localhost:$CrlPort"
        'KRYPTONIAN__TLS__CRLONLYHTTP'            = 'true'
        'KRYPTONIAN__TLS__CLIENTCAFILES__0'       = $CaPemPath
        'KRYPTONIAN__DATABASE__PROVIDER'          = 'Sqlite'
        # Program.cs reads GetConnectionString("DefaultConnection") FIRST and appsettings.json
        # ships a PostgreSQL value there, so this key is required; the Kryptonian:Database:*
        # fallback below would never be consulted.
        'CONNECTIONSTRINGS__DEFAULTCONNECTION'    = "Data Source=$DbPath"
        'KRYPTONIAN__DATABASE__CONNECTIONSTRING'  = "Data Source=$DbPath"
        # Both JWT consumers read configuration before falling back to the environment
        # variable, and appsettings.json ships an empty JwtSecret string that satisfies the
        # config lookup. Setting the nested key is what actually replaces it.
        'KRYPTONIAN__AUTH__JWTSECRET'             = $script:DevSecrets.jwtSecret
        'KRYPTONIAN__ADMINAPI__APIKEYS'           = $script:DevSecrets.adminApiKey
        'KRYPTONIAN__CA__SELFSIGNED__PFXPATH'     = $CaPfxPath
        'KRYPTONIAN__CA__SELFSIGNED__PFXPASSWORD' = $script:DevSecrets.caPfxPassword
        # Bind HTTPS with our loopback dev cert instead of the dotnet dev-certs certificate.
        'Kestrel__Certificates__Default__Path'     = $ServerPfx
        'Kestrel__Certificates__Default__Password' = $script:DevSecrets.pfxPassword
    }

    foreach ($name in $envMap.Keys) {
        $savedEnv[$name] = [Environment]::GetEnvironmentVariable($name)
        [Environment]::SetEnvironmentVariable($name, $envMap[$name])
    }

    Write-Host ''
    Write-Host "Kryptonian dev gateway: $BaseUrl" -ForegroundColor Green
    Write-Host "  Dashboard $BaseUrl/"
    Write-Host "  EST       $BaseUrl/.well-known/est/simpleenroll"
    Write-Host "  Health    $BaseUrl/api/status/health"
    Write-Host "  CRL HTTP  http://localhost:$CrlPort/api/crl/{issuer-SHA256}.crl (CRL-only listener)"
    Write-Host "  Database  $DbPath"
    Write-Host "  CA public $CaPemPath"
    Write-Host ''
    Write-Caution 'First run: open the dashboard, complete the admin setup wizard, then add a CA backend and an EST profile.'
    Write-Caution 'This launcher does not seed accounts or configuration, and does not modify OS trust stores.'
    Write-Caution 'Alternatively, run scripts/verify-dev.ps1 in another terminal for synthetic EST/DICOM setup and verification.'
    Write-Caution 'Your browser will warn about the untrusted dev certificate. Press Ctrl+C to stop.'
    Write-Host ''

    Push-Location $ApiProject
    try {
        # Foreground: dotnet inherits the process environment set above. --no-build is
        # safe because the build step ran earlier in this script.
        & dotnet run --project $ApiProject --no-launch-profile --no-build
        exit $LASTEXITCODE
    } finally {
        Pop-Location
    }
} finally {
    foreach ($name in $savedEnv.Keys) {
        [Environment]::SetEnvironmentVariable($name, $savedEnv[$name])
    }
}
