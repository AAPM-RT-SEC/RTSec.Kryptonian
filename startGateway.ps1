#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Start the Kryptonian gateway (API) locally via `dotnet run`.

.DESCRIPTION
    Launches src/RTSec.Kryptonian.Api with the admin UI/API on
    http://localhost:5000 and EST over HTTPS on https://localhost:8443.
    Redirects stdout/stderr to api.local.out.log and api.local.err.log in
    the repo root. Skips startup if the HTTPS gateway health check is already
    reachable.

.PARAMETER Foreground
    Run dotnet in the foreground instead of detaching.

.PARAMETER Force
    Start a new instance even if the gateway is already responding on :5000.

.EXAMPLE
    .\startGateway.ps1
    .\startGateway.ps1 -Foreground
#>

param(
    [switch]$Foreground,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$RepoRoot   = Split-Path -Parent $MyInvocation.MyCommand.Path
$ApiProject = Join-Path $RepoRoot 'src\RTSec.Kryptonian.Api'
$OutLog     = Join-Path $RepoRoot 'api.local.out.log'
$ErrLog     = Join-Path $RepoRoot 'api.local.err.log'
$AdminUrl   = 'http://localhost:5000'
$EstUrl     = 'https://localhost:8443'
$HealthUrl  = "$EstUrl/api/status/health"

function Test-GatewayUp {
    try {
        $r = Invoke-WebRequest -Uri $HealthUrl -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
        return $r.StatusCode -eq 200
    } catch {
        return $false
    }
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "Error: dotnet is not installed or not in PATH." -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $ApiProject)) {
    Write-Host "Error: API project not found at $ApiProject" -ForegroundColor Red
    exit 1
}

if (-not $Force -and (Test-GatewayUp)) {
    Write-Host "Kryptonian gateway is already running at $AdminUrl (EST: $EstUrl)" -ForegroundColor Green
    exit 0
}

$certCheck = & dotnet dev-certs https --check --trust 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: no trusted ASP.NET Core HTTPS development certificate was found." -ForegroundColor Red
    Write-Host "Run: dotnet dev-certs https --trust"
    Write-Host $certCheck
    exit 1
}

$env:ASPNETCORE_URLS        = "$AdminUrl;$EstUrl"
$env:ASPNETCORE_ENVIRONMENT = if ($env:ASPNETCORE_ENVIRONMENT) { $env:ASPNETCORE_ENVIRONMENT } else { 'Development' }

# Admin API key — must match VITE_API_KEY baked into src/RTSec.Kryptonian.Ui/.env.local
if (-not $env:KRYPTONIAN__ADMINAPI__APIKEYS) {
    $env:KRYPTONIAN__ADMINAPI__APIKEYS = 'kryptonian-local-dev-key'
}

if ($Foreground) {
    Write-Host "Starting gateway in foreground (Ctrl+C to stop)..." -ForegroundColor Cyan
    Write-Host "Project: $ApiProject"
    Write-Host "Admin:   $AdminUrl"
    Write-Host "EST:     $EstUrl"
    Push-Location $ApiProject
    try {
        dotnet run --no-launch-profile
    } finally {
        Pop-Location
    }
    exit $LASTEXITCODE
}

Write-Host "Starting gateway in background..." -ForegroundColor Cyan
Write-Host "Project:  $ApiProject"
Write-Host "Admin:    $AdminUrl"
Write-Host "EST:      $EstUrl"
Write-Host "Stdout ->  $OutLog"
Write-Host "Stderr ->  $ErrLog"

# Truncate existing logs so tailing them shows only this run.
Set-Content -Path $OutLog -Value '' -NoNewline
Set-Content -Path $ErrLog -Value '' -NoNewline

$proc = Start-Process -FilePath 'dotnet' `
    -ArgumentList @('run', '--no-launch-profile', '--project', $ApiProject) `
    -WorkingDirectory $RepoRoot `
    -RedirectStandardOutput $OutLog `
    -RedirectStandardError $ErrLog `
    -WindowStyle Hidden `
    -PassThru

Write-Host "Started dotnet (PID $($proc.Id)). Waiting for health check..." -ForegroundColor Cyan

$deadline = (Get-Date).AddSeconds(60)
while ((Get-Date) -lt $deadline) {
    if ($proc.HasExited) {
        Write-Host "dotnet exited early (code $($proc.ExitCode)). See $ErrLog" -ForegroundColor Red
        exit 1
    }
    if (Test-GatewayUp) {
        Write-Host "Gateway is up at $AdminUrl (EST: $EstUrl, PID $($proc.Id))" -ForegroundColor Green
        Write-Host "Tail logs:  Get-Content '$OutLog' -Wait"
        Write-Host "Stop:       Stop-Process -Id $($proc.Id)"
        exit 0
    }
    Start-Sleep -Milliseconds 750
}

Write-Host "Timed out waiting for $HealthUrl. Process still running (PID $($proc.Id)); check $OutLog / $ErrLog" -ForegroundColor Yellow
exit 1
