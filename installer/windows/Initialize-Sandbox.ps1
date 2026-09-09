# MSI machine configuration. No interactive-user trust-store changes.
[CmdletBinding()]
param([switch]$ProvisionOnly)
$ErrorActionPreference = 'Stop'
function Export-PublicEstCa([string]$Source, [string]$Destination) {
    # Replace by rename rather than writing through an existing public-file hard link.
    $target = [IO.Path]::GetFullPath($Destination)
    $directory = [IO.DirectoryInfo]::new([IO.Path]::GetDirectoryName($target))
    for ($parent = $directory; $null -ne $parent; $parent = $parent.Parent) {
        if ($parent.Exists -and ($parent.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Public Documents must not be a link.' }
    }
    [IO.Directory]::CreateDirectory($directory.FullName) | Out-Null
    if ((Test-Path -LiteralPath $target) -and ((Get-Item -LiteralPath $target -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Public EST CA must not be a link.' }
    $temporary = Join-Path $directory.FullName ([Guid]::NewGuid().ToString() + '.tmp')
    try {
        $stream = [IO.File]::Open($temporary, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        try {
        # Re-export public certificate data only, never a private key or PFX.
        $ca = [Security.Cryptography.X509Certificates.X509Certificate2]::CreateFromPem([IO.File]::ReadAllText($Source))
        try {
            $bytes = [Text.Encoding]::ASCII.GetBytes($ca.ExportCertificatePem())
            $stream.Write($bytes)
        } finally { $ca.Dispose() }
        } finally { $stream.Dispose() }
        $fileAcl = [Security.AccessControl.FileSecurity]::new()
        $fileAcl.SetAccessRuleProtection($true, $false)
        foreach ($sid in @('S-1-5-18','S-1-5-32-544', [Security.Principal.WindowsIdentity]::GetCurrent().User.Value) | Select-Object -Unique) {
            $fileAcl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new($sid), 'FullControl', 'Allow'))
        }
        $fileAcl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new('S-1-5-32-545'), 'ReadAndExecute', 'Allow'))
        Set-Acl -LiteralPath $temporary -AclObject $fileAcl
        [IO.File]::Move($temporary, $target, $true)
    } finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force } }
}
$principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Machine configuration must run from the elevated installer.' }
$state = Join-Path $env:ProgramData 'KryptonianSandbox'
if (Test-Path $state) {
    if ((Get-Item $state -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'State directory must not be a link.' }
} else { New-Item -ItemType Directory $state | Out-Null }
$acl = [Security.AccessControl.DirectorySecurity]::new()
$acl.SetAccessRuleProtection($true, $false)
foreach ($sid in @('S-1-5-18','S-1-5-32-544','S-1-5-19')) {
    $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new($sid), 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
}
Set-Acl -LiteralPath $state -AclObject $acl
if ($ProvisionOnly) { exit 0 }
Start-Service KryptonianSandbox
$deadline = [DateTime]::UtcNow.AddSeconds(90)
do {
    if ((Get-Service KryptonianSandbox).Status -eq 'Stopped') { throw "Gateway stopped. See $state\logs." }
    if (Test-Path (Join-Path $state 'admin-ca.pem')) { break }
    Start-Sleep -Seconds 1
} while ([DateTime]::UtcNow -lt $deadline)
if (-not (Test-Path (Join-Path $state 'admin-ca.pem'))) { throw 'Gateway initialization timed out.' }
$healthy = $false
do {
    # Explicit issuer + hostname validation without changing SYSTEM's certificate store.
    $status = & "$env:SystemRoot\System32\curl.exe" --silent --show-error --output NUL --write-out '%{http_code}' --max-time 3 --noproxy '*' --cacert (Join-Path $state 'admin-ca.pem') --ssl-no-revoke 'https://localhost:7445/api/status/health' 2>$null
    $healthy = $LASTEXITCODE -eq 0 -and $status -eq '200'
    if ($healthy) { break }
    Start-Sleep -Seconds 1
} while ([DateTime]::UtcNow -lt $deadline)
if (-not $healthy) { throw "Administration HTTPS health failed. See $state\logs; check for a conflict on ports 7443, 7444 or 7445." }
# Export only the public admin issuer into a read-only-for-users directory. Never
# loosen access on private keys, database, API credentials or raw test evidence.
$public = Join-Path $env:ProgramData 'KryptonianGatewayPublic'
if (Test-Path $public) {
    if ((Get-Item $public -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Public certificate directory must not be a link.' }
} else { New-Item -ItemType Directory $public | Out-Null }
$publicAcl = [Security.AccessControl.DirectorySecurity]::new()
$publicAcl.SetAccessRuleProtection($true, $false)
$publicAcl.SetOwner([Security.Principal.SecurityIdentifier]::new('S-1-5-18'))
foreach ($sid in @('S-1-5-18','S-1-5-32-544')) {
    $publicAcl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new($sid), 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
}
$publicAcl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new('S-1-5-32-545'), 'ReadAndExecute', 'ContainerInherit,ObjectInherit', 'None', 'Allow'))
Set-Acl -LiteralPath $public -AclObject $publicAcl
$publicCert = Join-Path $public 'admin-ca.pem'
if ((Test-Path $publicCert) -and ((Get-Item $publicCert -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Public certificate must not be a link.' }
Copy-Item -LiteralPath (Join-Path $state 'admin-ca.pem') -Destination $publicCert -Force
$certificateAcl = [Security.AccessControl.FileSecurity]::new()
$certificateAcl.SetOwner([Security.Principal.SecurityIdentifier]::new('S-1-5-18'))
$certificateAcl.SetAccessRuleProtection($false, $false)
Set-Acl -LiteralPath $publicCert -AclObject $certificateAcl
$estPublicCertificate = Join-Path ([Environment]::GetFolderPath('CommonDocuments')) 'Kryptonian-EST-CA.crt'
Export-PublicEstCa -Source (Join-Path $state 'ca.pem') -Destination $estPublicCertificate
Write-Host "Public EST CA exported to $estPublicCertificate"
Write-Host 'Gateway ready. Open Kryptonian Gateway from the Start Menu to create your administrator account.'
