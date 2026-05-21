// Bicep template for the public Kryptonian gateway.
//
// What this provisions in rg-kryptonian-hackathon:
//   1. Container app `kryptonian-gateway` running the image from
//      kryptonianregistry.azurecr.io, with secrets for the admin API key,
//      the harness team token, and the Postgres connection string.
//   2. AcrPull role assignment so the container app's system-assigned identity
//      can pull from kryptonianregistry.
//
// Database (Postgres flexible-server `kryptonian-pg`) is provisioned outside
// this template by deploy.ps1 because flex-server creation has long-running
// async semantics that don't compose cleanly with the rest of the deploy.
//
// Re-runnable: every resource uses a stable name and Bicep handles the
// existing-vs-create branches. Updating `imageTag` does a rolling deploy.

targetScope = 'resourceGroup'

@description('Image tag to deploy, e.g. "v2" or "latest".')
param imageTag string = 'v2'

@description('Strong random API key for the admin endpoints. Generated outside Bicep and passed as a parameter.')
@secure()
param adminApiKey string

@description('Team token issued by the CA harness (POST /api/teams/register). Used by the seed script to compose backend URLs.')
@secure()
param harnessTeamToken string

@description('CA harness public base URL.')
param harnessBaseUrl string = 'https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io'

@description('Postgres connection string in Npgsql format (Host=...;Database=...;Username=...;Password=...;SslMode=Require).')
@secure()
param postgresConnectionString string

// ── References to existing resources ──────────────────────────────────────────
resource acaEnv 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
  name: 'kryptonian-cae'
}

resource registry 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' existing = {
  name: 'kryptonianregistry'
}

// ── Container app ─────────────────────────────────────────────────────────────
resource gatewayApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: 'kryptonian-gateway'
  location: resourceGroup().location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    managedEnvironmentId: acaEnv.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 5000
        transport: 'auto'
        allowInsecure: false
      }
      registries: [
        {
          server: registry.properties.loginServer
          identity: 'system'
        }
      ]
      secrets: [
        {
          name: 'admin-api-key'
          value: adminApiKey
        }
        {
          name: 'harness-team-token'
          value: harnessTeamToken
        }
        {
          name: 'pg-conn'
          value: postgresConnectionString
        }
      ]
    }
    template: {
      revisionSuffix: 'r${replace(imageTag, '.', '-')}'
      containers: [
        {
          name: 'gateway'
          image: '${registry.properties.loginServer}/kryptonian-gateway:${imageTag}'
          resources: {
            cpu: json('0.5')
            memory: '1.0Gi'
          }
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:5000'
            }
            {
              // ACA terminates TLS at the ingress; trust the X-Forwarded-* headers
              // so issued Location URLs use https:// and HttpsRedirection is happy.
              name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED'
              value: 'true'
            }
            {
              name: 'Kryptonian__Database__Provider'
              value: 'PostgreSQL'
            }
            {
              name: 'ConnectionStrings__DefaultConnection'
              secretRef: 'pg-conn'
            }
            {
              name: 'Kryptonian__Est__AllowPlainHttp'
              value: 'true'
            }
            {
              name: 'Kryptonian__Tls__RedirectHttpToHttps'
              value: 'false'
            }
            {
              name: 'Kryptonian__AdminApi__ApiKeys'
              secretRef: 'admin-api-key'
            }
            {
              name: 'KRYPTONIAN_HARNESS_BASE_URL'
              value: harnessBaseUrl
            }
            {
              name: 'KRYPTONIAN_HARNESS_TEAM_TOKEN'
              secretRef: 'harness-team-token'
            }
          ]
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/api/status/health'
                port: 5000
              }
              periodSeconds: 30
              failureThreshold: 5
              initialDelaySeconds: 30
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/api/status/health'
                port: 5000
              }
              periodSeconds: 10
              failureThreshold: 3
              initialDelaySeconds: 15
            }
          ]
        }
      ]
      // Postgres handles the multi-writer coordination, so we can scale out
      // safely later. Cap at 3 for now to keep the bill predictable.
      scale: {
        minReplicas: 1
        maxReplicas: 3
      }
    }
  }
}

// ── AcrPull role assignment on the registry for the app's system identity ────
// The role definition ID below is the built-in AcrPull role.
var acrPullRoleId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'

resource acrPullRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: registry
  name: guid(registry.id, gatewayApp.id, acrPullRoleId)
  properties: {
    principalId: gatewayApp.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
  }
}

// ── Outputs ───────────────────────────────────────────────────────────────────
output gatewayFqdn string = gatewayApp.properties.configuration.ingress.fqdn
output gatewayUrl string = 'https://${gatewayApp.properties.configuration.ingress.fqdn}'
output containerAppName string = gatewayApp.name
