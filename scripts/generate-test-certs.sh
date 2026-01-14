#!/bin/bash
#
# Generate test certificates for Kryptonian development
#
# This script creates:
# - Root CA certificate and key
# - Server certificate for the API
# - Client certificate for EST testing
#
# Usage: ./generate-test-certs.sh [output_dir]
#

set -e

# Configuration
OUTPUT_DIR="${1:-./certs}"
CA_DAYS=3650
CERT_DAYS=365
CA_KEY_SIZE=4096
CERT_KEY_SIZE=2048
PFX_PASSWORD="${CA_PFX_PASSWORD:-TestPassword123!}"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check for OpenSSL
if ! command -v openssl &> /dev/null; then
    log_error "OpenSSL is not installed. Please install OpenSSL first."
    exit 1
fi

# Create output directory
mkdir -p "$OUTPUT_DIR"
cd "$OUTPUT_DIR"

log_info "Generating test certificates in: $(pwd)"

# =============================================================================
# Root CA
# =============================================================================
log_info "Generating Root CA..."

# Generate Root CA private key
openssl genrsa -out ca.key $CA_KEY_SIZE 2>/dev/null

# Create Root CA config
cat > ca.cnf << EOF
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
EOF

# Generate Root CA certificate
openssl req -x509 -new -nodes \
    -key ca.key \
    -sha256 \
    -days $CA_DAYS \
    -out ca.crt \
    -config ca.cnf

# Create PFX for self-signed backend
openssl pkcs12 -export \
    -out ca.pfx \
    -inkey ca.key \
    -in ca.crt \
    -passout pass:"$PFX_PASSWORD"

log_info "Root CA created: ca.crt, ca.key, ca.pfx"

# =============================================================================
# Server Certificate (for API TLS)
# =============================================================================
log_info "Generating Server certificate..."

# Generate server private key
openssl genrsa -out server.key $CERT_KEY_SIZE 2>/dev/null

# Create server config
cat > server.cnf << EOF
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
EOF

# Generate server CSR
openssl req -new \
    -key server.key \
    -out server.csr \
    -config server.cnf

# Create extensions file for server cert
cat > server_ext.cnf << EOF
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
EOF

# Sign server certificate with CA
openssl x509 -req \
    -in server.csr \
    -CA ca.crt \
    -CAkey ca.key \
    -CAcreateserial \
    -out server.crt \
    -days $CERT_DAYS \
    -sha256 \
    -extfile server_ext.cnf

# Create server PFX
openssl pkcs12 -export \
    -out server.pfx \
    -inkey server.key \
    -in server.crt \
    -certfile ca.crt \
    -passout pass:"$PFX_PASSWORD"

log_info "Server certificate created: server.crt, server.key, server.pfx"

# =============================================================================
# Client Certificate (for EST mTLS testing)
# =============================================================================
log_info "Generating Client certificate..."

# Generate client private key
openssl genrsa -out client.key $CERT_KEY_SIZE 2>/dev/null

# Create client config
cat > client.cnf << EOF
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
EOF

# Generate client CSR
openssl req -new \
    -key client.key \
    -out client.csr \
    -config client.cnf

# Create extensions file for client cert
cat > client_ext.cnf << EOF
authorityKeyIdentifier = keyid,issuer
basicConstraints = CA:FALSE
keyUsage = critical, digitalSignature, keyEncipherment
extendedKeyUsage = clientAuth
EOF

# Sign client certificate with CA
openssl x509 -req \
    -in client.csr \
    -CA ca.crt \
    -CAkey ca.key \
    -CAcreateserial \
    -out client.crt \
    -days $CERT_DAYS \
    -sha256 \
    -extfile client_ext.cnf

# Create client PFX
openssl pkcs12 -export \
    -out client.pfx \
    -inkey client.key \
    -in client.crt \
    -certfile ca.crt \
    -passout pass:"$PFX_PASSWORD"

log_info "Client certificate created: client.crt, client.key, client.pfx"

# =============================================================================
# Test CSR (for enrollment testing)
# =============================================================================
log_info "Generating test CSR for enrollment testing..."

# Generate test key
openssl genrsa -out test-enroll.key $CERT_KEY_SIZE 2>/dev/null

# Create test CSR config
cat > test-enroll.cnf << EOF
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
EOF

# Generate test CSR (PEM format)
openssl req -new \
    -key test-enroll.key \
    -out test-enroll.csr \
    -config test-enroll.cnf

# Generate test CSR (DER format for EST)
openssl req -new \
    -key test-enroll.key \
    -out test-enroll.der \
    -config test-enroll.cnf \
    -outform DER

# Base64 encode for EST (RFC 7030 format)
base64 -w 0 test-enroll.der > test-enroll.b64

log_info "Test CSR created: test-enroll.csr, test-enroll.der, test-enroll.b64"

# =============================================================================
# Cleanup temporary files
# =============================================================================
rm -f *.cnf *.csr *.srl

# =============================================================================
# Summary
# =============================================================================
echo ""
log_info "=========================================="
log_info "Certificate generation complete!"
log_info "=========================================="
echo ""
echo "Generated files:"
echo "  CA:     ca.crt, ca.key, ca.pfx"
echo "  Server: server.crt, server.key, server.pfx"
echo "  Client: client.crt, client.key, client.pfx"
echo "  Test:   test-enroll.key, test-enroll.der, test-enroll.b64"
echo ""
echo "PFX Password: $PFX_PASSWORD"
echo ""
log_warn "These certificates are for TESTING ONLY!"
log_warn "Do NOT use in production environments."
