#!/usr/bin/env bash
set -euo pipefail

# Run once on a fresh Ubuntu 22.04 VM to install Docker and start services.
# Usage: sudo bash setup.sh <CA_HARNESS_API_KEY> <ACR_PASSWORD>

CA_HARNESS_API_KEY="${1:?CA_HARNESS_API_KEY required}"
ACR_PASSWORD="${2:?ACR_PASSWORD required}"
ACR_SERVER="kryptonianregistry.azurecr.io"
ACR_USER="kryptonianregistry"

# Install Docker
apt-get update -q
apt-get install -y -q docker.io docker-compose-v2
systemctl enable --now docker

# Log in to ACR
echo "$ACR_PASSWORD" | docker login "$ACR_SERVER" -u "$ACR_USER" --password-stdin

# Write .env
cat > /opt/kryptonian/.env <<EOF
CA_HARNESS_API_KEY=${CA_HARNESS_API_KEY}
EOF

# Pull and start
docker compose -f /opt/kryptonian/docker-compose.yml --env-file /opt/kryptonian/.env pull
docker compose -f /opt/kryptonian/docker-compose.yml --env-file /opt/kryptonian/.env up -d

echo "Services started. Orthanc: http://$(curl -s ifconfig.me):8042  DIMSE proxy cert: http://$(curl -s ifconfig.me):8044/server-cert"
