#!/bin/bash
#
# Demo: Medical Device Certificate Enrollment Flow
#
# This script demonstrates the complete EST enrollment flow:
# 1. Start the system (optional)
# 2. Configure a CA backend
# 3. Create an EST profile
# 4. Simulate a medical device requesting a certificate
# 5. Show the issued certificate and audit log
#
# Usage: ./demo-enrollment.sh [--start-services] [--device-name NAME]
#

set -e

# =============================================================================
# Configuration
# =============================================================================
BASE_URL="${BASE_URL:-http://localhost:5000}"
API_KEY="${API_KEY:-dev-api-key-change-in-production}"
PFX_PASSWORD="${CA_PFX_PASSWORD:-TestPassword123!}"
DEVICE_NAME="${DEVICE_NAME:-CT-Scanner-001}"
DEMO_DIR="./demo-output"

# Parse arguments
START_SERVICES=false
while [[ $# -gt 0 ]]; do
    case $1 in
        --start-services)
            START_SERVICES=true
            shift
            ;;
        --device-name)
            DEVICE_NAME="$2"
            shift 2
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m'

# =============================================================================
# Helper Functions
# =============================================================================
print_header() {
    echo ""
    echo -e "${BOLD}${BLUE}════════════════════════════════════════════════════════════════${NC}"
    echo -e "${BOLD}${BLUE}  $1${NC}"
    echo -e "${BOLD}${BLUE}════════════════════════════════════════════════════════════════${NC}"
    echo ""
}

print_step() {
    echo -e "${CYAN}▶ $1${NC}"
}

print_substep() {
    echo -e "  ${YELLOW}→${NC} $1"
}

print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

print_error() {
    echo -e "${RED}✗ $1${NC}"
}

print_device() {
    echo -e "${BOLD}${GREEN}[DEVICE]${NC} $1"
}

print_hub() {
    echo -e "${BOLD}${YELLOW}[HUB]${NC} $1"
}

print_admin() {
    echo -e "${BOLD}${BLUE}[ADMIN]${NC} $1"
}

wait_for_keypress() {
    echo ""
    echo -e "${BOLD}Press Enter to continue...${NC}"
    read -r
}

check_command() {
    if ! command -v "$1" &> /dev/null; then
        print_error "$1 is required but not installed."
        exit 1
    fi
}

# =============================================================================
# Prerequisites Check
# =============================================================================
print_header "Checking Prerequisites"

check_command curl
check_command openssl
check_command jq
print_success "All required tools are installed"

# Create demo output directory
mkdir -p "$DEMO_DIR"

# =============================================================================
# Start Services (Optional)
# =============================================================================
if [ "$START_SERVICES" = true ]; then
    print_header "Starting Services"

    print_step "Starting Docker Compose..."
    docker compose up -d

    print_step "Waiting for services to be healthy..."
    sleep 10
fi

# =============================================================================
# Health Check
# =============================================================================
print_header "Step 0: Verify System Health"

print_step "Checking API health..."
HEALTH_RESPONSE=$(curl -s "$BASE_URL/api/status/health")
echo "Health status: $HEALTH_RESPONSE"

# Handle both string "Healthy" and JSON {"status": "Healthy"} responses
if [[ "$HEALTH_RESPONSE" == *"Healthy"* ]]; then
    print_success "System is healthy!"
else
    print_error "System is not healthy. Please start services first."
    echo "Run: docker compose up -d"
    exit 1
fi

wait_for_keypress

# =============================================================================
# Step 1: Configure CA Backend (Admin)
# =============================================================================
print_header "Step 1: Administrator Configures CA Backend"

print_admin "Creating a Self-Signed CA backend..."
print_substep "This tells Kryptonian which CA to use for signing certificates"

CA_RESPONSE=$(curl -s -X POST "$BASE_URL/api/cas" \
    -H "X-API-Key: $API_KEY" \
    -H "Content-Type: application/json" \
    -d '{
        "name": "Medical Device CA",
        "type": "selfsigned",
        "config": {
            "PfxPath": "/app/certs/ca.pfx",
            "PfxPassword": "'"$PFX_PASSWORD"'"
        },
        "isEnabled": true
    }')

# Check if CA already exists (409 Conflict) or was created
if echo "$CA_RESPONSE" | jq -e '.id' > /dev/null 2>&1; then
    CA_BACKEND_ID=$(echo "$CA_RESPONSE" | jq -r '.id')
    print_success "CA Backend created with ID: $CA_BACKEND_ID"
else
    # Try to get existing CA
    print_substep "CA may already exist, fetching..."
    CA_LIST=$(curl -s "$BASE_URL/api/cas" -H "X-API-Key: $API_KEY")
    CA_BACKEND_ID=$(echo "$CA_LIST" | jq -r '.[0].id')
    print_success "Using existing CA Backend: $CA_BACKEND_ID"
fi

echo ""
echo "CA Backend Response:"
echo "$CA_RESPONSE" | jq .

wait_for_keypress

# =============================================================================
# Step 2: Create EST Profile (Admin)
# =============================================================================
print_header "Step 2: Administrator Creates EST Profile"

print_admin "Creating an EST Profile..."
print_substep "This defines the enrollment endpoint and links it to the CA"

PROFILE_RESPONSE=$(curl -s -X POST "$BASE_URL/api/est-profiles" \
    -H "X-API-Key: $API_KEY" \
    -H "Content-Type: application/json" \
    -d '{
        "name": "Medical Device Enrollment",
        "pathPrefix": "/.well-known/est",
        "hostnames": ["localhost"],
        "caBackendId": "'"$CA_BACKEND_ID"'",
        "validityDays": 365,
        "requireClientCertificate": false,
        "isEnabled": true
    }')

if echo "$PROFILE_RESPONSE" | jq -e '.id' > /dev/null 2>&1; then
    PROFILE_ID=$(echo "$PROFILE_RESPONSE" | jq -r '.id')
    print_success "EST Profile created with ID: $PROFILE_ID"
else
    # Try to get existing profile
    print_substep "Profile may already exist, fetching..."
    PROFILE_LIST=$(curl -s "$BASE_URL/api/est-profiles" -H "X-API-Key: $API_KEY")
    PROFILE_ID=$(echo "$PROFILE_LIST" | jq -r '.[0].id')
    print_success "Using existing EST Profile: $PROFILE_ID"
fi

echo ""
echo "EST Profile Response:"
echo "$PROFILE_RESPONSE" | jq .

wait_for_keypress

# =============================================================================
# Step 3: Device Gets CA Certificates
# =============================================================================
print_header "Step 3: Device Asks 'Who Will Sign My Certificate?'"

print_device "Requesting CA certificates from the hub..."
print_hub "Returning the CA certificate chain"

curl -s "$BASE_URL/.well-known/est/cacerts" \
    -H "Accept: application/pkcs7-mime" \
    -o "$DEMO_DIR/ca_response.b64"

# Decode and display
echo ""
print_step "Decoding CA certificate..."
base64 -d "$DEMO_DIR/ca_response.b64" > "$DEMO_DIR/ca_response.der" 2>/dev/null || \
    cat "$DEMO_DIR/ca_response.b64" | tr -d '\r\n' | base64 -d > "$DEMO_DIR/ca_response.der"

openssl pkcs7 -in "$DEMO_DIR/ca_response.der" -inform DER -print_certs -out "$DEMO_DIR/ca_chain.pem" 2>/dev/null

echo ""
echo -e "${BOLD}CA Certificate Info:${NC}"
openssl x509 -in "$DEMO_DIR/ca_chain.pem" -noout -subject -issuer -dates 2>/dev/null || echo "(Could not parse CA cert)"

print_success "Device now knows which CA to trust!"

wait_for_keypress

# =============================================================================
# Step 4: Device Generates Key Pair and CSR
# =============================================================================
print_header "Step 4: Device Generates Key Pair and Certificate Request"

print_device "Generating private key..."
openssl genrsa -out "$DEMO_DIR/device.key" 2048 2>/dev/null
print_success "Private key generated (kept secret on device)"

print_device "Creating Certificate Signing Request (CSR)..."
print_substep "Device name: $DEVICE_NAME"

openssl req -new \
    -key "$DEMO_DIR/device.key" \
    -out "$DEMO_DIR/device.csr" \
    -subj "/CN=$DEVICE_NAME/O=Hospital System/OU=Radiology/C=US" \
    2>/dev/null

# Convert to DER and base64 for EST
openssl req -in "$DEMO_DIR/device.csr" -outform DER -out "$DEMO_DIR/device.csr.der" 2>/dev/null
base64 -w 0 "$DEMO_DIR/device.csr.der" > "$DEMO_DIR/device.csr.b64" 2>/dev/null || \
    base64 "$DEMO_DIR/device.csr.der" | tr -d '\n' > "$DEMO_DIR/device.csr.b64"

echo ""
echo -e "${BOLD}CSR Contents:${NC}"
openssl req -in "$DEMO_DIR/device.csr" -noout -subject 2>/dev/null

print_success "CSR ready to send to hub"

wait_for_keypress

# =============================================================================
# Step 5: Device Requests Certificate
# =============================================================================
print_header "Step 5: Device Requests Certificate from Hub"

print_device "Sending CSR to hub: 'Please give me a certificate'"
print_hub "Validating request..."
print_hub "Forwarding to CA backend..."
print_hub "Returning signed certificate"

echo ""
print_step "Sending enrollment request..."

HTTP_CODE=$(curl -s -w "%{http_code}" -X POST "$BASE_URL/.well-known/est/simpleenroll" \
    -H "Content-Type: application/pkcs10" \
    -H "Content-Transfer-Encoding: base64" \
    -H "X-Device-Id: $DEVICE_NAME" \
    --data-binary @"$DEMO_DIR/device.csr.b64" \
    -o "$DEMO_DIR/cert_response.b64")

echo ""
if [ "$HTTP_CODE" = "200" ]; then
    print_success "Certificate issued! (HTTP $HTTP_CODE)"

    # Decode the response
    base64 -d "$DEMO_DIR/cert_response.b64" > "$DEMO_DIR/cert_response.der" 2>/dev/null || \
        cat "$DEMO_DIR/cert_response.b64" | tr -d '\r\n' | base64 -d > "$DEMO_DIR/cert_response.der"

    openssl pkcs7 -in "$DEMO_DIR/cert_response.der" -inform DER -print_certs -out "$DEMO_DIR/device_cert.pem" 2>/dev/null

    echo ""
    echo -e "${BOLD}${GREEN}═══════════════════════════════════════════════════════════════${NC}"
    echo -e "${BOLD}${GREEN}  ISSUED CERTIFICATE${NC}"
    echo -e "${BOLD}${GREEN}═══════════════════════════════════════════════════════════════${NC}"
    echo ""
    openssl x509 -in "$DEMO_DIR/device_cert.pem" -noout -subject -issuer -dates -serial 2>/dev/null
    echo ""

else
    print_error "Enrollment failed (HTTP $HTTP_CODE)"
    echo "Response:"
    cat "$DEMO_DIR/cert_response.b64"
    exit 1
fi

wait_for_keypress

# =============================================================================
# Step 6: Device Installs Certificate
# =============================================================================
print_header "Step 6: Device Installs Certificate"

print_device "Combining certificate with private key..."

openssl pkcs12 -export \
    -out "$DEMO_DIR/device.pfx" \
    -inkey "$DEMO_DIR/device.key" \
    -in "$DEMO_DIR/device_cert.pem" \
    -passout pass:"device-password" \
    2>/dev/null

print_success "Certificate bundle created: demo-output/device.pfx"
print_success "Device is now ready for secure communication!"

echo ""
echo "Files created:"
echo "  - demo-output/device.key      (Private key - keep secret!)"
echo "  - demo-output/device_cert.pem (Public certificate)"
echo "  - demo-output/device.pfx      (Combined bundle)"

wait_for_keypress

# =============================================================================
# Step 7: View Audit Log
# =============================================================================
print_header "Step 7: Administrator Views Enrollment Audit Log"

print_admin "Checking enrollment events..."

EVENTS=$(curl -s "$BASE_URL/api/status/enrollments?limit=5" \
    -H "X-API-Key: $API_KEY")

echo ""
echo -e "${BOLD}Recent Enrollment Events:${NC}"
echo "$EVENTS" | jq '.[] | {timestamp, deviceId, status, subjectDn}'

print_success "Enrollment complete and logged!"

# =============================================================================
# Summary
# =============================================================================
print_header "Demo Complete!"

echo -e "${BOLD}What Just Happened:${NC}"
echo ""
echo "  1. Admin configured a CA backend (the actual certificate authority)"
echo "  2. Admin created an EST profile (enrollment endpoint configuration)"
echo "  3. Device asked hub: 'Who will sign my certificate?'"
echo "  4. Device generated a key pair and certificate request"
echo "  5. Device sent request to hub, hub forwarded to CA, certificate issued"
echo "  6. Device installed the certificate"
echo "  7. Admin can see the audit trail"
echo ""
echo -e "${BOLD}Key Point:${NC} The device only talked to Kryptonian (the hub)."
echo "           It never communicated directly with the CA."
echo ""
echo -e "${BOLD}Files created in demo-output/:${NC}"
ls -la "$DEMO_DIR"
echo ""
