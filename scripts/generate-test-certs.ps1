<#
.SYNOPSIS
    Generate test certificates for Kryptonian development

.DESCRIPTION
    Creates test CA, server, and client certificates for local development.
    Requires OpenSSL to be installed (winget install OpenSSL.Light)

.PARAMETER OutputDir
    Directory to output certificates (default: ../certs relative to this script)

.EXAMPLE
    .\generate-test-certs.ps1
    .\generate-test-certs.ps1 -OutputDir "C:\certs"
#>

param(
    [string]$OutputDir
)

# Get the script's directory and set default output relative to it
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $OutputDir) {
    $OutputDir = Join-Path (Split-Path -Parent $ScriptDir) "certs"
}

# Configuration
$CA_DAYS = 3650
$CERT_DAYS = 365
$CA_KEY_SIZE = 4096
$CERT_KEY_SIZE = 2048
$PFX_PASSWORD = if ($env:CA_PFX_PASSWORD) { $env:CA_PFX_PASSWORD } else { "TestPassword123!" }

# Check for OpenSSL
if (-not (Get-Command openssl -ErrorAction SilentlyContinue)) {
    Write-Host "[ERROR] OpenSSL is not installed." -ForegroundColor Red
    Write-Host ""
    Write-Host "Install OpenSSL using one of these methods:" -ForegroundColor Yellow
    Write-Host "  winget install OpenSSL.Light"
    Write-Host "  choco install openssl"
    Write-Host ""
    exit 1
}

# Create output directory
$OutputDir = [System.IO.Path]::GetFullPath($OutputDir)
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
Push-Location $OutputDir

Write-Host ""
Write-Host "Generating test certificates in: $OutputDir" -ForegroundColor Cyan
Write-Host ""

# =============================================================================
# Root CA
# =============================================================================
Write-Host "[1/4] Generating Root CA..." -ForegroundColor Green

# Generate Root CA private key
& openssl genrsa -out ca.key $CA_KEY_SIZE 2>$null

# Create Root CA config
@"
[req]
default_bits = $CA_KEY_SIZE
prompt = no
default_md = sha256
distinguished_name = dn
x509_extensions = v3_ca

[dn]
C = US
ST = California
L = San Francisco
O = Kryptonian Test
OU = Certificate Authority
CN = Kryptonian Test Root CA

[v3_ca]
subjectKeyIdentifier = hash
authorityKeyIdentifier = keyid:always,issuer
basicConstraints = critical, CA:TRUE, pathlen:1
keyUsage = critical, digitalSignature, cRLSign, keyCertSign
"@ | Out-File -FilePath ca.cnf -Encoding ASCII

# Generate Root CA certificate
& openssl req -x509 -new -nodes -key ca.key -sha256 -days $CA_DAYS -out ca.crt -config ca.cnf 2>$null

# Create PFX for self-signed backend
& openssl pkcs12 -export -out ca.pfx -inkey ca.key -in ca.crt -passout pass:$PFX_PASSWORD 2>$null

Write-Host "       Created: ca.crt, ca.key, ca.pfx" -ForegroundColor Gray

# =============================================================================
# Server Certificate
# =============================================================================
Write-Host "[2/4] Generating Server certificate..." -ForegroundColor Green

# Generate server private key
& openssl genrsa -out server.key $CERT_KEY_SIZE 2>$null

# Create server config
@"
[req]
default_bits = $CERT_KEY_SIZE
prompt = no
default_md = sha256
distinguished_name = dn
req_extensions = req_ext

[dn]
C = US
ST = California
L = San Francisco
O = Kryptonian Test
OU = Server
CN = localhost

[req_ext]
subjectAltName = @alt_names

[alt_names]
DNS.1 = localhost
DNS.2 = kryptonian-api
DNS.3 = api
IP.1 = 127.0.0.1
IP.2 = ::1
"@ | Out-File -FilePath server.cnf -Encoding ASCII

# Generate server CSR
& openssl req -new -key server.key -out server.csr -config server.cnf 2>$null

# Create extensions file
@"
authorityKeyIdentifier = keyid,issuer
basicConstraints = CA:FALSE
keyUsage = critical, digitalSignature, keyEncipherment
extendedKeyUsage = serverAuth
subjectAltName = @alt_names

[alt_names]
DNS.1 = localhost
DNS.2 = kryptonian-api
DNS.3 = api
IP.1 = 127.0.0.1
IP.2 = ::1
"@ | Out-File -FilePath server_ext.cnf -Encoding ASCII

# Sign server certificate
& openssl x509 -req -in server.csr -CA ca.crt -CAkey ca.key -CAcreateserial -out server.crt -days $CERT_DAYS -sha256 -extfile server_ext.cnf 2>$null

# Create server PFX
& openssl pkcs12 -export -out server.pfx -inkey server.key -in server.crt -certfile ca.crt -passout pass:$PFX_PASSWORD 2>$null

Write-Host "       Created: server.crt, server.key, server.pfx" -ForegroundColor Gray

# =============================================================================
# Client Certificate
# =============================================================================
Write-Host "[3/4] Generating Client certificate..." -ForegroundColor Green

# Generate client private key
& openssl genrsa -out client.key $CERT_KEY_SIZE 2>$null

# Create client config
@"
[req]
default_bits = $CERT_KEY_SIZE
prompt = no
default_md = sha256
distinguished_name = dn

[dn]
C = US
ST = California
L = San Francisco
O = Kryptonian Test
OU = Device
CN = test-device-001
"@ | Out-File -FilePath client.cnf -Encoding ASCII

# Generate client CSR
& openssl req -new -key client.key -out client.csr -config client.cnf 2>$null

# Create extensions file
@"
authorityKeyIdentifier = keyid,issuer
basicConstraints = CA:FALSE
keyUsage = critical, digitalSignature, keyEncipherment
extendedKeyUsage = clientAuth
"@ | Out-File -FilePath client_ext.cnf -Encoding ASCII

# Sign client certificate
& openssl x509 -req -in client.csr -CA ca.crt -CAkey ca.key -CAcreateserial -out client.crt -days $CERT_DAYS -sha256 -extfile client_ext.cnf 2>$null

# Create client PFX
& openssl pkcs12 -export -out client.pfx -inkey client.key -in client.crt -certfile ca.crt -passout pass:$PFX_PASSWORD 2>$null

Write-Host "       Created: client.crt, client.key, client.pfx" -ForegroundColor Gray

# =============================================================================
# Test CSR for enrollment testing
# =============================================================================
Write-Host "[4/4] Generating test CSR for enrollment..." -ForegroundColor Green

# Generate test key
& openssl genrsa -out test-enroll.key $CERT_KEY_SIZE 2>$null

# Create test CSR config
@"
[req]
default_bits = $CERT_KEY_SIZE
prompt = no
default_md = sha256
distinguished_name = dn

[dn]
C = US
ST = California
L = San Francisco
O = Test Organization
OU = Test Device
CN = test-device-enroll
"@ | Out-File -FilePath test-enroll.cnf -Encoding ASCII

# Generate test CSR (PEM and DER format)
& openssl req -new -key test-enroll.key -out test-enroll.csr -config test-enroll.cnf 2>$null
& openssl req -new -key test-enroll.key -out test-enroll.der -config test-enroll.cnf -outform DER 2>$null

# Base64 encode for EST
$derBytes = [System.IO.File]::ReadAllBytes("$OutputDir\test-enroll.der")
[System.Convert]::ToBase64String($derBytes) | Out-File -FilePath test-enroll.b64 -Encoding ASCII -NoNewline

Write-Host "       Created: test-enroll.key, test-enroll.csr, test-enroll.b64" -ForegroundColor Gray

# =============================================================================
# Cleanup
# =============================================================================
Remove-Item *.cnf -ErrorAction SilentlyContinue
Remove-Item *.csr -ErrorAction SilentlyContinue
Remove-Item *.srl -ErrorAction SilentlyContinue

Pop-Location

# =============================================================================
# Summary
# =============================================================================
Write-Host ""
Write-Host "==========================================" -ForegroundColor Green
Write-Host " Certificate generation complete!" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Generated files in $OutputDir`:"
Write-Host "  CA:     ca.crt, ca.key, ca.pfx"
Write-Host "  Server: server.crt, server.key, server.pfx"
Write-Host "  Client: client.crt, client.key, client.pfx"
Write-Host "  Test:   test-enroll.key, test-enroll.der, test-enroll.b64"
Write-Host ""
Write-Host "PFX Password: $PFX_PASSWORD" -ForegroundColor Yellow
Write-Host ""
Write-Host "WARNING: These certificates are for TESTING ONLY!" -ForegroundColor Red
Write-Host "         Do NOT use in production environments." -ForegroundColor Red
Write-Host ""
