# Non-mutating package checks; does not install the MSI or trust certificates.
[CmdletBinding()]
param([string]$Package = (Join-Path $PSScriptRoot '../../artifacts/windows-installer/windows-release/KryptonianGateway.msi'))
$ErrorActionPreference = 'Stop'
Get-ChildItem $PSScriptRoot -Filter '*.ps1' | ForEach-Object {
    $tokens = $null; $errors = $null
    [void][Management.Automation.Language.Parser]::ParseFile($_.FullName, [ref]$tokens, [ref]$errors)
    if ($errors.Count) { throw "Invalid PowerShell: $($_.Name): $errors" }
}
$installer = New-Object -ComObject WindowsInstaller.Installer
$database = $installer.OpenDatabase((Resolve-Path -LiteralPath $Package).Path, 0)
$view = $database.OpenView('SELECT `Name`, `StartName`, `Arguments`, `StartType` FROM `ServiceInstall`')
$view.Execute()
$row = $view.Fetch()
if (-not $row -or $row.StringData(1) -ne 'KryptonianSandbox' -or $row.StringData(2) -ne 'NT AUTHORITY\LocalService' -or
    $row.StringData(3) -ne '--Kryptonian:Sandbox:Enabled=true' -or $row.StringData(4) -ne '2') { throw 'Incorrect service identity, arguments or startup mode.' }
$view.Close()
$view = $database.OpenView('SELECT `FileName` FROM `File`')
$view.Execute()
$files = @()
while ($row = $view.Fetch()) { $files += ($row.StringData(1) -split '\|')[-1] }
$view.Close()
foreach ($name in @('RTSec.Kryptonian.Api.exe','Kryptonian.DICOMTls.exe','pwsh.exe','index.html','Initialize-Sandbox.ps1','Open-Gateway.ps1','Run-Demo.ps1','verify-dev.ps1')) {
    if ($name -notin $files) { throw "Missing installed file: $name" }
}
if ($files | Where-Object { $_ -match '(?i)\.(pfx|key|db)$|^secrets\.json$' }) { throw 'Private state packaged in MSI.' }
if ($files | Where-Object { $_ -match '^(Launch-Sandbox|Sandbox-Bootstrap)\.' }) { throw 'Windows Sandbox launcher must not be installed.' }
$view = $database.OpenView('SELECT `Action`, `Type`, `Source`, `Target` FROM `CustomAction`')
$view.Execute()
$actions = @{}
while ($row = $view.Fetch()) { $actions[$row.StringData(1)] = @($row.StringData(2),$row.StringData(3),$row.StringData(4)) }
$view.Close()
foreach ($action in @('KryptonianProvisionState','KryptonianVerifyReady')) {
    if (-not $actions.ContainsKey($action) -or $actions[$action][0] -ne '3090' -or $actions[$action][1] -ne 'pwsh.exe') { throw "Missing elevated deferred installer action: $action" }
}
$view = $database.OpenView('SELECT `Action`, `Sequence`, `Condition` FROM `InstallExecuteSequence`')
$view.Execute()
$sequence = @{}
while ($row = $view.Fetch()) { $sequence[$row.StringData(1)] = [int]$row.StringData(2) }
$view.Close()
if (-not ($sequence.InstallFiles -lt $sequence.KryptonianProvisionState -and
    $sequence.KryptonianProvisionState -lt $sequence.StartServices -and
    $sequence.StartServices -lt $sequence.KryptonianVerifyReady -and
    $sequence.KryptonianVerifyReady -lt $sequence.InstallFinalize)) { throw 'Unsafe machine provisioning/service readiness order.' }
Write-Host "PASS: script parsing, MSI service contract and $($files.Count) payload files; no packaged private state."
