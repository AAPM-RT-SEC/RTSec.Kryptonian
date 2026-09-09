<#
.SYNOPSIS
    Runs the packaged Kryptonian sandbox end-to-end demo and records plain-text evidence.

.DESCRIPTION
    Invokes the bundled verifier with the packaged PowerShell 7 runtime, the self-contained
    DICOM demo executable, and the service-owned state directory. Nothing is installed, no
    trust store is touched, and no service is started or stopped by this script.

    Evidence (log + result summary) is written under the protected state directory. No private
    material is exported OUTSIDE that directory: the verifier stores password-protected
    client.pfx and renewed.pfx inside its verify-* evidence folder there, alongside secrets
    and CA keys already owned by the service. The raw log is NOT sanitized and may echo
    child-process output; only the result summary file is restricted to identifiers, URLs and
    statuses.

.PARAMETER AppDirectory
    Package root containing tools\pwsh\pwsh.exe, tools\dicom\Kryptonian.DICOMTls.exe and
    scripts\verify-dev.ps1. Defaults to this script's own folder, because Run-Demo.ps1 is
    installed at the package root.

.PARAMETER DataDirectory
    Initialized gateway state directory (contains secrets.json, ca.pem, ca.pfx, server.pfx,
    admin-ca.pem). Override for an isolated parent test run.

.PARAMETER Port / AdminPort / CrlPort
    Device EST, admin API and CRL listener ports. Defaults 7443 / 7445 / 7444.

.EXAMPLE
    .\Run-Demo.ps1
    .\Run-Demo.ps1 -Port 8443 -AdminPort 8445 -CrlPort 8444 -DataDirectory 'C:\Temp\kryp-sandbox'
#>

[CmdletBinding()]
param(
    [string]$AppDirectory,
    [string]$DataDirectory = (Join-Path $env:ProgramData 'KryptonianSandbox'),
    [ValidateRange(1024, 65535)] [int]$Port = 7443,
    [ValidateRange(1024, 65535)] [int]$AdminPort = 7445,
    [ValidateRange(1024, 65535)] [int]$CrlPort = 7444
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# The demonstration deliberately uses protected administrative test credentials.
# Request UAC explicitly instead of failing later with filesystem access denied.
$principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if ($DataDirectory -eq (Join-Path $env:ProgramData 'KryptonianSandbox') -and
    -not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    try {
        $demoProcess = Start-Process -FilePath (Join-Path $PSHOME 'pwsh.exe') -Verb RunAs -Wait -PassThru -ArgumentList @(
            '-NoProfile', '-NoExit', '-ExecutionPolicy', 'Bypass', '-File', ('"' + $PSCommandPath + '"'))
        exit $demoProcess.ExitCode
    } catch { Write-Error 'The demonstration requires administrator approval. No test was run.'; exit 1 }
}

if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw "PowerShell 7+ required (found $($PSVersionTable.PSVersion))."
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
# Run-Demo.ps1 is installed at the package root, so its own folder IS APPDIR.
if (-not $AppDirectory) { $AppDirectory = $scriptDir }
$AppDirectory = [IO.Path]::GetFullPath($AppDirectory)
$state = [IO.Path]::GetFullPath($DataDirectory)

$pwsh   = Join-Path $AppDirectory 'tools\pwsh\pwsh.exe'
$dicom  = Join-Path $AppDirectory 'tools\dicom\Kryptonian.DICOMTls.exe'
$script = Join-Path $AppDirectory 'scripts\verify-dev.ps1'

foreach ($required in @(@('bundled PowerShell', $pwsh), @('verifier', $script), @('DICOM executable', $dicom))) {
    if (-not (Test-Path -LiteralPath $required[1])) {
        Write-Host "MISSING $($required[0]): $($required[1])" -ForegroundColor Red
        exit 2
    }
}
foreach ($file in @('secrets.json', 'ca.pem', 'admin-ca.pem')) {
    if (-not (Test-Path -LiteralPath (Join-Path $state $file))) {
        Write-Host "MISSING state file: $(Join-Path $state $file)" -ForegroundColor Red
        exit 2
    }
}

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$logPath   = Join-Path $state "demo-$stamp.log"
$resultPath = Join-Path $state "demo-$stamp.result.txt"

Write-Host 'Kryptonian sandbox demo' -ForegroundColor Cyan
Write-Host "  EST         https://localhost:$Port"
Write-Host "  Admin API   https://localhost:$AdminPort"
Write-Host "  CRL         http://localhost:$CrlPort"
Write-Host "  State       $state"
Write-Host "  Log         $logPath"
Write-Host ''
Write-Host 'Running enrollment, renewal, revocation and DICOM TLS checks...' -ForegroundColor Cyan

$verifierArgs = @(
    '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $script
    '-Port', $Port, '-AdminPort', $AdminPort, '-CrlPort', $CrlPort
    '-DataDirectory', $state
    '-AdminCaPath', (Join-Path $state 'admin-ca.pem')
    '-DicomExecutable', $dicom
    '-CheckRevocation'
)

$exitCode = 0
try {
    & $pwsh @verifierArgs *>&1 | Tee-Object -FilePath $logPath | ForEach-Object {
        $line = "$_"
        # Surface human-readable progress; suppress noise but never a failure line.
        if ($line -match '^(PASS|FAIL|Checking|Developer evidence|.*failed)') { Write-Host $line }
        else { Write-Verbose $line }
    }
    $exitCode = $LASTEXITCODE
} catch {
    Write-Host "DEMO ERROR: $($_.Exception.Message)" -ForegroundColor Red
    $exitCode = 1
}

# Result summary carries identifiers, endpoints and statuses only. It is not a substitute for
# sanitizing the raw log, which is unsanitized child output and stays inside protected state.
$status = if ($exitCode -eq 0) { 'PASSED' } else { 'FAILED' }
@(
    "Kryptonian sandbox demo: $status"
    "Finished:      $(Get-Date -Format 'o')"
    "Exit code:     $exitCode"
    "EST port:      $Port"
    "Admin port:    $AdminPort"
    "CRL port:      $CrlPort"
    "State:         $state"
    "Full log:      $logPath"
    "Evidence dir:  $(Join-Path $state 'verify-*')"
) | Set-Content -LiteralPath $resultPath -Encoding utf8

Write-Host ''
if ($exitCode -eq 0) {
    Write-Host "Demo PASSED. Summary: $resultPath" -ForegroundColor Green
} else {
    Write-Host "Demo FAILED (exit $exitCode). See $logPath" -ForegroundColor Red
}
# Evidence is retained on both paths; nothing here deletes prior runs.
exit $exitCode
