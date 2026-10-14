# Build-machine only. The resulting package needs no developer tools or downloads.
[CmdletBinding()]
param(
    [string]$AdvancedInstaller = ${env:ADVINST_COM},
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '0.1.4',
    [switch]$SkipPublish
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$build = Join-Path $repo 'artifacts/windows-installer'
$payload = Join-Path $build 'payload'
$release = Join-Path $build 'windows-release'
if (-not $AdvancedInstaller) { $AdvancedInstaller = 'C:\Program Files (x86)\Caphyon\Advanced Installer 22.0\bin\x86\AdvancedInstaller.com' }
$AdvancedInstaller = $AdvancedInstaller.Trim('"')
if (-not (Test-Path -LiteralPath $AdvancedInstaller)) { throw 'Specify -AdvancedInstaller pointing to AdvancedInstaller.com.' }
New-Item -ItemType Directory -Force $payload,$release | Out-Null
function Run([string]$Exe, [string[]]$Arguments) {
    & $Exe @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Exe failed (exit $LASTEXITCODE)." }
}
if (-not $SkipPublish) {
    Push-Location (Join-Path $repo 'src/RTSec.Kryptonian.Ui')
    try { Run npm.cmd @('ci','--no-audit','--no-fund'); Run npm.cmd @('run','build') } finally { Pop-Location }
    Run dotnet @('publish', (Join-Path $repo 'src/RTSec.Kryptonian.Api'), '-c','Release','-r','win-x64','--self-contained','true','--artifacts-path',(Join-Path $build 'dotnet'),'-o',$payload)
    Run dotnet @('publish', (Join-Path $repo 'src/Kryptonian.DICOMTls'), '-c','Release','-r','win-x64','--self-contained','true','--artifacts-path',(Join-Path $build 'dotnet'),'-o',(Join-Path $payload 'tools/dicom'))
    New-Item -ItemType Directory -Force (Join-Path $payload 'wwwroot/ui') | Out-Null
    Copy-Item (Join-Path $repo 'src/RTSec.Kryptonian.Ui/dist/*') (Join-Path $payload 'wwwroot/ui') -Recurse -Force
}
# Official portable runtime, pinned to the upstream release SHA256 (not the host's modules/profile).
$archive = Join-Path $build 'PowerShell-7.6.5-win-x64.zip'
$expected = '32EB8F6CDCE08F86E987D625A2733E54AC3E289AE7E1621B14C0B5BCEC2434EA'
if (-not (Test-Path $archive)) {
    Invoke-WebRequest 'https://github.com/PowerShell/PowerShell/releases/download/v7.6.5/PowerShell-7.6.5-win-x64.zip' -OutFile $archive
}
if ((Get-FileHash $archive -Algorithm SHA256).Hash -ne $expected) { throw 'Portable PowerShell checksum mismatch; refusing to package.' }
$portable = Join-Path $payload 'tools/pwsh'
if (-not (Test-Path (Join-Path $portable 'pwsh.exe'))) { Expand-Archive -LiteralPath $archive -DestinationPath $portable }
New-Item -ItemType Directory -Force (Join-Path $payload 'scripts'),(Join-Path $payload 'docs') | Out-Null
Copy-Item (Join-Path $repo 'scripts/verify-dev.ps1') (Join-Path $payload 'scripts') -Force
Copy-Item (Join-Path $PSScriptRoot 'Run-Demo.ps1'),(Join-Path $PSScriptRoot 'Initialize-Sandbox.ps1'),(Join-Path $PSScriptRoot 'Open-Gateway.ps1') $payload -Force
Copy-Item (Join-Path $repo 'docs/windows-installation.md'),(Join-Path $repo 'docs/windows-sandbox.md') (Join-Path $payload 'docs') -Force
Copy-Item (Join-Path $repo 'LICENSE') $payload -Force
foreach ($required in @('RTSec.Kryptonian.Api.exe','wwwroot/ui/index.html','tools/dicom/Kryptonian.DICOMTls.exe','tools/pwsh/pwsh.exe')) {
    if (-not (Test-Path (Join-Path $payload $required))) { throw "Missing payload: $required" }
}
if (Get-ChildItem $payload -Recurse -File | Where-Object { $_.Extension -in '.pfx','.key','.db' -or $_.Name -eq 'secrets.json' }) { throw 'Private state found in installer payload.' }
$project = Join-Path $PSScriptRoot 'KryptonianGateway.aip'
Run $AdvancedInstaller @('/edit',$project,'/SetVersion',$Version)
Run $AdvancedInstaller @('/edit',$project,'/RefreshSync','APPDIR')
[xml]$definition = Get-Content -Raw $project
$service = $definition.SelectSingleNode('//ROW[@ServiceInstall="KryptonianSandbox"]')
if ($null -eq $service -or $service.Arguments -ne '--Kryptonian:Sandbox:Enabled=true' -or $service.StartName -ne 'NT AUTHORITY\LocalService') {
    throw 'Installer service definition missing or unsafe; refusing to build.'
}
Run $AdvancedInstaller @('/build',$project)
& (Join-Path $PSScriptRoot 'Test-Package.ps1') -Package (Join-Path $release 'KryptonianGateway.msi')
Copy-Item (Join-Path $repo 'docs/windows-installation.md') (Join-Path $release 'README.md') -Force
$distributionFiles = @('KryptonianGateway.msi','README.md','SHA256SUMS.txt') |
    ForEach-Object { Join-Path $release $_ }
$distributionFiles | Where-Object { [IO.Path]::GetFileName($_) -ne 'SHA256SUMS.txt' } | Get-FileHash -Algorithm SHA256 |
    ForEach-Object { '{0}  {1}' -f $_.Hash, [IO.Path]::GetFileName($_.Path) } | Set-Content (Join-Path $release 'SHA256SUMS.txt')
Compress-Archive -LiteralPath $distributionFiles -DestinationPath (Join-Path $build "KryptonianGateway-$Version-win-x64.zip") -Force
Write-Host "Windows installer release: $release"
Write-Warning 'Local preview is unsigned unless signing is configured in the AIP. Sign and revalidate before public distribution.'
