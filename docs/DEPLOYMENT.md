# RTSec.Kryptonian Deployment Guide

This guide covers deploying RTSec.Kryptonian in development, testing, and production environments.

## Prerequisites

- Docker 24.0+ and Docker Compose 2.20+
- .NET 8.0 SDK (for local development)
- OpenSSL (for certificate generation)
- PostgreSQL 16+ (or use Docker)

## Quick Start (Development)

### 1. Generate Test Certificates

```bash
# Make the script executable
chmod +x scripts/generate-test-certs.sh

# Generate certificates (output to ./certs)
./scripts/generate-test-certs.sh

# Or specify a custom output directory
./scripts/generate-test-certs.sh /path/to/certs
```

This creates:
- `ca.crt`, `ca.key`, `ca.pfx` - Root CA certificate and key
- `server.crt`, `server.key`, `server.pfx` - Server TLS certificate
- `client.crt`, `client.key`, `client.pfx` - Client certificate for EST testing
- `test-enroll.key`, `test-enroll.der`, `test-enroll.b64` - Test CSR for enrollment

### 2. Start with Docker Compose

```bash
# Start PostgreSQL and API
docker compose up -d

# Start with Admin Web UI (optional)
docker compose --profile web up -d

# View logs
docker compose logs -f api
```

### 3. Verify Deployment

```bash
# Health check
curl http://localhost:5000/api/status/health

# Expected response
# {"status":"Healthy","components":{"database":"Healthy","ca_connectors":"Healthy"}}
```

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `POSTGRES_PASSWORD` | PostgreSQL password | `kryptonian_dev_password` |
| `ASPNETCORE_ENVIRONMENT` | Environment (Development/Production) | `Development` |
| `ADMIN_API_KEYS` | Comma-separated API keys for admin access | `dev-api-key-change-in-production` |
| `CA_PFX_PASSWORD` | Password for CA PFX file | `TestPassword123!` |

### Database Configuration

The connection string is configured via environment variable:

```bash
ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=kryptonian;Username=kryptonian;Password=your_password"
```

For Docker Compose, this is automatically configured to connect to the PostgreSQL container.

### CA Backend Configuration

#### Self-Signed CA

Configure via environment variables:

```bash
KRYPTONIAN__CA__SELFSIGNED__PFXPATH=/app/certs/ca.pfx
KRYPTONIAN__CA__SELFSIGNED__PFXPASSWORD=your_pfx_password
```

Or using separate cert/key files:

```bash
KRYPTONIAN__CA__SELFSIGNED__CERTPATH=/app/certs/ca.crt
KRYPTONIAN__CA__SELFSIGNED__KEYPATH=/app/certs/ca.key
```

#### ACME CA (Let's Encrypt)

Create an ACME backend via the Admin API:

```bash
curl -X POST http://localhost:5000/api/cas \
  -H "X-API-Key: your-api-key" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "LetsEncrypt Staging",
    "type": "acme",
    "config": {
      "DirectoryUrl": "https://acme-staging-v02.api.letsencrypt.org/directory",
      "Email": "admin@example.com",
      "PreferredChallengeType": "http-01"
    }
  }'
```

## Production Deployment

### Security Checklist

1. **Change default credentials**
   ```bash
   # Set strong passwords
   export POSTGRES_PASSWORD="$(openssl rand -base64 32)"
   export ADMIN_API_KEYS="$(openssl rand -hex 32)"
   export CA_PFX_PASSWORD="$(openssl rand -base64 32)"
   ```

2. **Use production certificates**
   - Replace test certificates with properly signed certificates
   - Use a trusted CA for the server TLS certificate
   - Generate a proper CA certificate for signing device certificates

3. **HTTPS is MANDATORY for Admin UI/API**
   - The Admin Web UI will **fail to start** in production without HTTPS for `ApiBaseUrl`
   - API keys are transmitted in headers and MUST be protected by TLS
   - Configure a reverse proxy (nginx, Traefik) with TLS termination
   - Or configure Kestrel HTTPS directly:
   ```bash
   # Kestrel HTTPS configuration
   ASPNETCORE_URLS=https://+:5001
   Kestrel__Certificates__Default__Path=/app/certs/server.pfx
   Kestrel__Certificates__Default__Password=${SERVER_CERT_PASSWORD}
   ```

4. **API Key is MANDATORY**
   - The Admin Web UI will **fail to start** in production without an `ApiKey` configured
   - Use strong, randomly generated keys (minimum 32 characters)
   - Rotate keys periodically

5. **Rate limiting**
   - EST endpoints have built-in rate limiting (configurable in appsettings.json)
   - Consider additional rate limiting at the reverse proxy level
   - Monitor for abuse patterns in enrollment logs

6. **Network isolation**
   - Run the API on an internal network
   - Expose only necessary ports through a firewall
   - Use network policies in Kubernetes
   - **Admin API** (`/api/*`): Internal network only, never expose publicly
   - **EST endpoints** (`/.well-known/est/*`): Can be exposed with mTLS

7. **ACME HTTP-01 Challenge Security**
   - The `/.well-known/acme-challenge/` endpoint is exposed for domain validation
   - This endpoint is **unauthenticated** by design (ACME requirement)
   - Restrict access to only the ACME CA's IP ranges if possible
   - Consider using DNS-01 challenges instead for sensitive environments
   - The challenge tokens are ephemeral and single-use

8. **Secrets management**
   - Use Docker Secrets, Kubernetes Secrets, or a vault solution
   - Never commit secrets to version control
   - CA private keys should be stored encrypted at rest

### Docker Production Deployment

Create a `docker-compose.prod.yml`:

```yaml
version: '3.8'

services:
  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_USER: kryptonian
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_DB: kryptonian
    volumes:
      - postgres_data:/var/lib/postgresql/data
    networks:
      - internal
    restart: unless-stopped

  api:
    image: kryptonian-api:latest
    depends_on:
      postgres:
        condition: service_healthy
    environment:
      ConnectionStrings__DefaultConnection: "Host=postgres;Port=5432;Database=kryptonian;Username=kryptonian;Password=${POSTGRES_PASSWORD}"
      ASPNETCORE_ENVIRONMENT: Production
      Kryptonian__AdminApi__ApiKeys: ${ADMIN_API_KEYS}
      KRYPTONIAN__CA__SELFSIGNED__PFXPATH: /app/certs/ca.pfx
      KRYPTONIAN__CA__SELFSIGNED__PFXPASSWORD: ${CA_PFX_PASSWORD}
    volumes:
      - ./certs:/app/certs:ro
      - ./logs:/app/logs
    networks:
      - internal
      - external
    restart: unless-stopped

  nginx:
    image: nginx:alpine
    ports:
      - "443:443"
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf:ro
      - ./certs/server.crt:/etc/nginx/ssl/server.crt:ro
      - ./certs/server.key:/etc/nginx/ssl/server.key:ro
    depends_on:
      - api
    networks:
      - external
    restart: unless-stopped

volumes:
  postgres_data:

networks:
  internal:
    internal: true
  external:
```

### Kubernetes Deployment

Example deployment manifest:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: kryptonian-api
spec:
  replicas: 2
  selector:
    matchLabels:
      app: kryptonian-api
  template:
    metadata:
      labels:
        app: kryptonian-api
    spec:
      containers:
      - name: api
        image: kryptonian-api:latest
        ports:
        - containerPort: 5000
        env:
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: kryptonian-secrets
              key: database-connection-string
        - name: Kryptonian__AdminApi__ApiKeys
          valueFrom:
            secretKeyRef:
              name: kryptonian-secrets
              key: admin-api-keys
        volumeMounts:
        - name: certs
          mountPath: /app/certs
          readOnly: true
        livenessProbe:
          httpGet:
            path: /api/status/health
            port: 5000
          initialDelaySeconds: 10
          periodSeconds: 30
        readinessProbe:
          httpGet:
            path: /api/status/health
            port: 5000
          initialDelaySeconds: 5
          periodSeconds: 10
      volumes:
      - name: certs
        secret:
          secretName: kryptonian-certs
---
apiVersion: v1
kind: Service
metadata:
  name: kryptonian-api
spec:
  selector:
    app: kryptonian-api
  ports:
  - port: 5000
    targetPort: 5000
```

## Database Migrations

Migrations are applied automatically on startup in Development mode. For production:

```bash
# Generate migration script
dotnet ef migrations script --idempotent -o migration.sql \
  --project src/RTSec.Kryptonian.Infrastructure \
  --startup-project src/RTSec.Kryptonian.Api

# Apply manually or through CI/CD
psql -h localhost -U kryptonian -d kryptonian -f migration.sql
```

## Monitoring

### Health Checks

The API exposes a health check endpoint:

```bash
GET /api/status/health
```

Response:
```json
{
  "status": "Healthy",
  "components": {
    "database": "Healthy",
    "ca_connectors": "Healthy"
  }
}
```

### Logging

Logs are written to:
- Console (stdout) - for container environments
- `./logs/kryptonian-*.log` - rolling daily files

Configure log level via environment:

```bash
Serilog__MinimumLevel__Default=Information
Serilog__MinimumLevel__Override__Microsoft=Warning
```

### Metrics

For Prometheus metrics integration, add the following package and configure:

```bash
dotnet add package prometheus-net.AspNetCore
```

## Troubleshooting

### Common Issues

**Database connection failed**
```
Check PostgreSQL is running and accessible:
  docker compose logs postgres

Verify connection string:
  docker compose exec api printenv | grep Connection
```

**Certificate errors**
```
Regenerate certificates:
  ./scripts/generate-test-certs.sh

Check certificate permissions:
  ls -la certs/
```

**API returns 401 Unauthorized**
```
Verify API key is set correctly:
  curl -H "X-API-Key: your-api-key" http://localhost:5000/api/cas

Check ADMIN_API_KEYS environment variable
```

### Debug Mode

Enable detailed logging:

```bash
ASPNETCORE_ENVIRONMENT=Development
Serilog__MinimumLevel__Default=Debug
```

## API Reference

### Admin API Authentication

All admin API endpoints require the `X-API-Key` header:

```bash
curl -H "X-API-Key: your-api-key" http://localhost:5000/api/cas
```

### Common Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/status/health` | GET | Health check |
| `/api/cas` | GET/POST | List/create CA backends |
| `/api/cas/{id}` | GET/PUT/DELETE | Manage CA backend |
| `/api/est-profiles` | GET/POST | List/create EST profiles |
| `/api/est-profiles/{id}` | GET/PUT/DELETE | Manage EST profile |
| `/api/status/enrollments` | GET | List enrollment events |

### EST Protocol Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/.well-known/est/{profile}/cacerts` | GET | Get CA certificates |
| `/.well-known/est/{profile}/simpleenroll` | POST | Enroll new certificate |
| `/.well-known/est/{profile}/simplereenroll` | POST | Renew certificate |

## Support

For issues and feature requests, please open an issue on the project repository.
