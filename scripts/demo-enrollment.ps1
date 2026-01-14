<#
.SYNOPSIS
    Demo: Medical Device Certificate Enrollment Flow

.DESCRIPTION
    This script demonstrates the complete EST enrollment flow:
    1. Verify system health
    2. Configure a CA backend
    3. Create an EST profile
    4. Simulate a medical device requesting a certificate
    5. Show the issued certificate and audit log

.PARAMETER StartServices
    Start Docker Compose services before running demo

.PARAMETER DeviceName
    Name for the simulated device (default: CT-Scanner-001)

.EXAMPLE
    .\demo-enrollment.ps1
    .\demo-enrollment.ps1 -StartServices -DeviceName "MRI-Scanner-002"
#>

param(
    [switch]$StartServices,
    [string]$DeviceName = "CT-Scanner-001"
)

# =============================================================================
# Configuration
# =============================================================================
$ScriptDir = $PSScriptRoot
$BaseUrl = if ($env:BASE_URL) { $env:BASE_URL } else { "http://localhost:5000" }
$ApiKey = if ($env:API_KEY) { $env:API_KEY } else { "dev-api-key-change-in-production" }
$PfxPassword = if ($env:CA_PFX_PASSWORD) { $env:CA_PFX_PASSWORD } else { "TestPassword123!" }
$DemoDir = Join-Path $ScriptDir "..\demo-output"

# Create demo output directory
New-Item -ItemType Directory -Force -Path $DemoDir | Out-Null

# =============================================================================
# Helper Functions
# =============================================================================
function Write-Header($text) {
    Write-Host ""
    Write-Host ("=" * 70) -ForegroundColor Blue
    Write-Host "  $text" -ForegroundColor Blue
    Write-Host ("=" * 70) -ForegroundColor Blue
    Write-Host ""
}

function Write-Step($text) {
    Write-Host ">> $text" -ForegroundColor Cyan
}

function Write-Success($text) {
    Write-Host "[OK] $text" -ForegroundColor Green
}

function Write-Error($text) {
    Write-Host "[X] $text" -ForegroundColor Red
}

function Write-Device($text) {
    Write-Host "[DEVICE] " -ForegroundColor Green -NoNewline
    Write-Host $text
}

function Write-Hub($text) {
    Write-Host "[HUB] " -ForegroundColor Yellow -NoNewline
    Write-Host $text
}

function Write-Admin($text) {
    Write-Host "[ADMIN] " -ForegroundColor Blue -NoNewline
    Write-Host $text
}

function Wait-ForKeypress {
    Write-Host ""
    Write-Host "Press Enter to continue..." -ForegroundColor Gray
    Read-Host
}

# =============================================================================
# Prerequisites Check
# =============================================================================
Write-Header "Checking Prerequisites"

# Check for OpenSSL
if (-not (Get-Command openssl -ErrorAction SilentlyContinue)) {
    Write-Error "OpenSSL is required. Install via: winget install OpenSSL.Light"
    exit 1
}
Write-Success "OpenSSL found"

# =============================================================================
# Start Services (Optional)
# =============================================================================
if ($StartServices) {
    Write-Header "Starting Services"
    Write-Step "Starting Docker Compose..."
    docker compose up -d
    Write-Step "Waiting for services to start..."
    Start-Sleep -Seconds 10
}

# =============================================================================
# Health Check
# =============================================================================
Write-Header "Step 0: Verify System Health"

Write-Step "Checking API health..."
try {
    $health = Invoke-WebRequest -Uri "$BaseUrl/api/status/health" -Method Get -UseBasicParsing
    $healthText = $health.Content.Trim().Trim('"')
    Write-Host "Health status: $healthText"

    if ($healthText -eq "Healthy") {
        Write-Success "System is healthy!"
    } else {
        Write-Error "System is not healthy: $healthText"
        exit 1
    }
} catch {
    Write-Error "Cannot connect to API. Run: docker compose up -d"
    Write-Host $_.Exception.Message
    exit 1
}

Wait-ForKeypress

# =============================================================================
# Step 1: Configure CA Backend
# =============================================================================
Write-Header "Step 1: Administrator Configures CA Backend"

Write-Admin "Creating a Self-Signed CA backend..."
Write-Host "  -> This tells Kryptonian which CA to use for signing certificates"

$caBody = @{
    name = "Medical Device CA"
    type = "selfsigned"
    config = @{
        PfxPath = "/app/certs/ca.pfx"
        PfxPassword = $PfxPassword
    }
    isEnabled = $true
} | ConvertTo-Json

try {
    $caResponse = Invoke-RestMethod -Uri "$BaseUrl/api/cas" -Method Post `
        -Headers @{ "X-API-Key" = $ApiKey } `
        -ContentType "application/json" `
        -Body $caBody

    $caBackendId = $caResponse.id
    Write-Success "CA Backend created: $caBackendId"
} catch {
    # CA might already exist
    Write-Host "  -> CA may already exist, fetching..." -ForegroundColor Yellow
    $caList = Invoke-RestMethod -Uri "$BaseUrl/api/cas" -Method Get `
        -Headers @{ "X-API-Key" = $ApiKey }
    $caBackendId = $caList[0].id
    Write-Success "Using existing CA Backend: $caBackendId"
}

Wait-ForKeypress

# =============================================================================
# Step 2: Create EST Profile
# =============================================================================
Write-Header "Step 2: Administrator Creates EST Profile"

Write-Admin "Creating an EST Profile..."
Write-Host "  -> This defines the enrollment endpoint and links it to the CA"

$profileBody = @{
    name = "Medical Device Enrollment"
    pathPrefix = "/.well-known/est"
    hostnames = @("localhost")
    hostnameMatchType = "Exact"
    caBackendId = $caBackendId
    validityDays = 365
    requireClientCertificate = $false
    isEnabled = $true
} | ConvertTo-Json

try {
    $profileResponse = Invoke-RestMethod -Uri "$BaseUrl/api/est-profiles" -Method Post `
        -Headers @{ "X-API-Key" = $ApiKey } `
        -ContentType "application/json" `
        -Body $profileBody

    $profileId = $profileResponse.id
    Write-Success "EST Profile created: $profileId"
} catch {
    Write-Host "  -> Profile may already exist, fetching..." -ForegroundColor Yellow
    $profileList = Invoke-RestMethod -Uri "$BaseUrl/api/est-profiles" -Method Get `
        -Headers @{ "X-API-Key" = $ApiKey }
    $profileId = $profileList[0].id
    Write-Success "Using existing EST Profile: $profileId"
}

Wait-ForKeypress

# =============================================================================
# Step 3: Device Gets CA Certificates
# =============================================================================
Write-Header "Step 3: Device Asks 'Who Will Sign My Certificate?'"

Write-Device "Requesting CA certificates from the hub..."
Write-Hub "Returning the CA certificate chain"

$caResp = Invoke-WebRequest -Uri "$BaseUrl/.well-known/est/cacerts" -Method Get -UseBasicParsing
$caResp.Content | Out-File -FilePath "$DemoDir\ca_response.b64" -Encoding ASCII

Write-Success "Device now knows which CA to trust!"

Wait-ForKeypress

# =============================================================================
# Step 4: Device Generates Key Pair and CSR
# =============================================================================
Write-Header "Step 4: Device Generates Key Pair and Certificate Request"

Write-Device "Generating private key..."
& openssl genrsa -out "$DemoDir\device.key" 2048 2>$null
Write-Success "Private key generated (kept secret on device)"

Write-Device "Creating Certificate Signing Request (CSR)..."
Write-Host "  -> Device name: $DeviceName"

& openssl req -new `
    -key "$DemoDir\device.key" `
    -out "$DemoDir\device.csr" `
    -subj "/CN=$DeviceName/O=Hospital System/OU=Radiology/C=US" 2>$null

# Convert to DER and base64 for EST
& openssl req -in "$DemoDir\device.csr" -outform DER -out "$DemoDir\device.csr.der" 2>$null
$csrBytes = [System.IO.File]::ReadAllBytes("$DemoDir\device.csr.der")
$csrB64 = [System.Convert]::ToBase64String($csrBytes)
$csrB64 | Out-File -FilePath "$DemoDir\device.csr.b64" -Encoding ASCII -NoNewline

Write-Host ""
Write-Host "CSR Subject: CN=$DeviceName, O=Hospital System, OU=Radiology, C=US"
Write-Success "CSR ready to send to hub"

Wait-ForKeypress

# =============================================================================
# Step 5: Device Requests Certificate
# =============================================================================
Write-Header "Step 5: Device Requests Certificate from Hub"

Write-Device "Sending CSR to hub: 'Please give me a certificate'"
Write-Hub "Validating request..."
Write-Hub "Forwarding to CA backend..."
Write-Hub "Returning signed certificate"

Write-Host ""
Write-Step "Sending enrollment request..."

try {
    $enrollResponse = Invoke-WebRequest -Uri "$BaseUrl/.well-known/est/simpleenroll" -Method Post `
        -Headers @{
            "Content-Type" = "application/pkcs10"
            "Content-Transfer-Encoding" = "base64"
            "X-Device-Id" = $DeviceName
        } `
        -Body $csrB64 `
        -UseBasicParsing

    Write-Success "Certificate issued! (HTTP $($enrollResponse.StatusCode))"

    # Save and decode response
    # Handle both string and byte array responses
    $contentStr = if ($enrollResponse.Content -is [byte[]]) {
        [System.Text.Encoding]::UTF8.GetString($enrollResponse.Content)
    } else {
        $enrollResponse.Content
    }
    $contentStr | Out-File -FilePath "$DemoDir\cert_response.b64" -Encoding ASCII

    $certBytes = [System.Convert]::FromBase64String($contentStr.Trim())
    [System.IO.File]::WriteAllBytes("$DemoDir\cert_response.der", $certBytes)

    & openssl pkcs7 -in "$DemoDir\cert_response.der" -inform DER -print_certs -out "$DemoDir\device_cert.pem" 2>$null

    Write-Host ""
    Write-Host ("=" * 60) -ForegroundColor Green
    Write-Host "  ISSUED CERTIFICATE" -ForegroundColor Green
    Write-Host ("=" * 60) -ForegroundColor Green
    Write-Host ""
    & openssl x509 -in "$DemoDir\device_cert.pem" -noout -subject -issuer -dates -serial
    Write-Host ""

} catch {
    Write-Error "Enrollment failed: $($_.Exception.Message)"
    exit 1
}

Wait-ForKeypress

# =============================================================================
# Step 6: Device Installs Certificate
# =============================================================================
Write-Header "Step 6: Device Installs Certificate"

Write-Device "Combining certificate with private key..."

& openssl pkcs12 -export `
    -out "$DemoDir\device.pfx" `
    -inkey "$DemoDir\device.key" `
    -in "$DemoDir\device_cert.pem" `
    -passout pass:device-password 2>$null

Write-Success "Certificate bundle created: demo-output\device.pfx"
Write-Success "Device is now ready for secure communication!"

Write-Host ""
Write-Host "Files created:"
Write-Host "  - demo-output\device.key      (Private key - keep secret!)"
Write-Host "  - demo-output\device_cert.pem (Public certificate)"
Write-Host "  - demo-output\device.pfx      (Combined bundle)"

Wait-ForKeypress

# =============================================================================
# Step 7: View Audit Log
# =============================================================================
Write-Header "Step 7: Administrator Views Enrollment Audit Log"

Write-Admin "Checking enrollment events..."

$events = Invoke-RestMethod -Uri "$BaseUrl/api/status/enrollments?limit=5" -Method Get `
    -Headers @{ "X-API-Key" = $ApiKey }

Write-Host ""
Write-Host "Recent Enrollment Events:" -ForegroundColor White
$events | ForEach-Object {
    Write-Host "  - $($_.timestamp): $($_.deviceId) - $($_.status)" -ForegroundColor Gray
    Write-Host "    Subject: $($_.subjectDn)" -ForegroundColor Gray
}

Write-Success "Enrollment complete and logged!"

# =============================================================================
# Summary
# =============================================================================
Write-Header "Demo Complete!"

Write-Host "What Just Happened:" -ForegroundColor White
Write-Host ""
Write-Host "  1. Admin configured a CA backend (the actual certificate authority)"
Write-Host "  2. Admin created an EST profile (enrollment endpoint configuration)"
Write-Host "  3. Device asked hub: 'Who will sign my certificate?'"
Write-Host "  4. Device generated a key pair and certificate request"
Write-Host "  5. Device sent request to hub, hub forwarded to CA, certificate issued"
Write-Host "  6. Device installed the certificate"
Write-Host "  7. Admin can see the audit trail"
Write-Host ""
Write-Host "Key Point: " -ForegroundColor Yellow -NoNewline
Write-Host "The device only talked to Kryptonian (the hub)."
Write-Host "           It never communicated directly with the CA."
Write-Host ""
Write-Host "Files created in demo-output\:" -ForegroundColor White
Get-ChildItem $DemoDir | Format-Table Name, Length, LastWriteTime
