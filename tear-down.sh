#!/usr/bin/env bash

set -euo pipefail

REMOVE_VOLUMES=false
PROFILE="web"

usage() {
    cat <<'EOF'
Usage: ./tear-down.sh [options]

Stops and removes backend + frontend Docker Compose services.

Options:
  --volumes      Also remove persistent volumes (deletes local DB data)
  -h, --help     Show this help
EOF
}

if ! command -v docker >/dev/null 2>&1; then
    echo "Error: docker is not installed or not in PATH."
    exit 1
fi

while [[ $# -gt 0 ]]; do
    case "$1" in
        --volumes)
            REMOVE_VOLUMES=true
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

args=(--profile "$PROFILE" down --remove-orphans)
if [[ "$REMOVE_VOLUMES" == true ]]; then
    args+=( --volumes )
fi

echo "Stopping services with: docker compose ${args[*]}"
docker compose "${args[@]}"

echo
echo "Remaining compose services:"
docker compose ps
