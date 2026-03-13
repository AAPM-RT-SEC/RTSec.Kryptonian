#!/usr/bin/env bash

set -euo pipefail

PROFILE="web"
BUILD=true
DETACH=true

usage() {
    cat <<'EOF'
Usage: ./start-up.sh [options]

Starts database, API, and frontend (web profile) with Docker Compose.

Options:
  --no-build     Start without rebuilding images
  --no-detach    Run in foreground
  -h, --help     Show this help
EOF
}

if ! command -v docker >/dev/null 2>&1; then
    echo "Error: docker is not installed or not in PATH."
    exit 1
fi

while [[ $# -gt 0 ]]; do
    case "$1" in
        --no-build)
            BUILD=false
            shift
            ;;
        --no-detach)
            DETACH=false
            shift
            ;;
        -h|--help)
            usage
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            usage
            exit 1
            ;;
    esac
done

# Ensure CA_PFX_PASSWORD is set consistently for both cert generation and docker-compose.
# generate-test-certs.sh defaults to TestPassword123! when unset; docker-compose.yml uses
# ${CA_PFX_PASSWORD:-} which would pass an empty string, causing CA load failure at startup.
if [[ -z "${CA_PFX_PASSWORD:-}" ]]; then
    export CA_PFX_PASSWORD="TestPassword123!"
fi

# Generate local test certs once if missing.
if [[ ! -f "certs/ca.pfx" ]]; then
    if [[ -x "scripts/generate-test-certs.sh" ]]; then
        echo "Generating test certificates..."
        ./scripts/generate-test-certs.sh
    elif [[ -f "scripts/generate-test-certs.sh" ]]; then
        echo "Generating test certificates..."
        bash ./scripts/generate-test-certs.sh
    else
        echo "Warning: scripts/generate-test-certs.sh not found; continuing without generating certs."
    fi
fi

args=(--profile "$PROFILE" up)

if [[ "$DETACH" == true ]]; then
    args+=( -d )
fi

if [[ "$BUILD" == true ]]; then
    args+=( --build )
fi

echo "Starting services with: docker compose ${args[*]}"
docker compose "${args[@]}"

echo
echo "Service status:"
docker compose ps

echo
echo "Container health:"
for c in kryptonian-db kryptonian-api kryptonian-web; do
    if docker ps -a --format '{{.Names}}' | grep -qx "$c"; then
        health=$(docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$c" 2>/dev/null || true)
        echo "- $c: ${health:-unknown}"
    fi
done

echo
echo "URLs:"
echo "- API: http://localhost:5000"
echo "- API health: http://localhost:5000/api/status/health"
echo "- Web UI: http://localhost:5001"
