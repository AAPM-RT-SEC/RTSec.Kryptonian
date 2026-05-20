// Bicep template for the public Kryptonian gateway.
//
// What this provisions in rg-kryptonian-hackathon:
//   1. File share `kryptonian-gateway-data` on the existing stkryptonianfiles
//      storage account (SQLite file + logs survive container restarts here).
//   2. Container Apps environment storage binding pointing at that share.
//   3. Container app `kryptonian-gateway` running the image from
//      kryptonianregistry.azurecr.io, with secrets for the admin API key and
//      the harness team token, env vars selecting the SQLite provider, and a
//      volume mount at /data backed by the file share.
//   4. AcrPull role assignment so the container app's system-assigned identity
//      can pull from kryptonianregistry.
//
// Re-runnable: every resource uses a stable name and Bicep handles the
// existing-vs-create branches. Updating `imageTag` does a rolling deploy.

targetScope = 'resourceGroup'

@description('Image tag to deploy, e.g. "v1" or "latest".')
param imageTag string = 'v1'

@description('Strong random API key for the admin endpoints. Generated outside Bicep and passed as a parameter.')
@secure()
param adminApiKey string

@description('Team token issued by the CA harness (POST /api/teams/register). Used by the seed script to compose backend URLs.')
@secure()
param harnessTeamToken string

@description('CA harness public base URL.')
param harnessBaseUrl string = 'https://ca-harness.mangotree-b3d09362.eastus.azurecontainerapps.io'

// ── References to existing resources ──────────────────────────────────────────
resource acaEnv 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
  name: 'kryptonian-cae'
}

resource registry 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' existing = {
  name: 'kryptonianregistry'
}

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: 'stkryptonianfiles'
}

// ── File share for SQLite + logs ──────────────────────────────────────────────
resource fileShare 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-05-01' = {
  name: '${storage.name}/default/kryptonian-gateway-data'
  properties: {
    shareQuota: 5
    enabledProtocols: 'SMB'
    accessTier: 'Hot'
  }
}

// ── Container Apps environment storage binding ───────────────────────────────
resource acaStorage 'Microsoft.App/managedEnvironments/storages@2024-03-01' = {
  parent: acaEnv
  name: 'kryptonian-gateway-data'
  properties: {
    azureFile: {
      accountName: storage.name
      accountKey: storage.listKeys().keys[0].value
      shareName: 'kryptonian-gateway-data'
      accessMode: 'ReadWrite'
    }
  }
  dependsOn: [
    fileShare
  ]
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
              name: 'Kryptonian__Database__Provider'
              value: 'Sqlite'
            }
            {
              name: 'ConnectionStrings__DefaultConnection'
              value: 'Data Source=/data/kryptonian.db;Cache=Shared'
            }
            {
              name: 'Kryptonian__Est__AllowPlainHttp'
              value: 'true'
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
          volumeMounts: [
            {
              volumeName: 'data'
              mountPath: '/data'
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
              initialDelaySeconds: 20
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/api/status/health'
                port: 5000
              }
              periodSeconds: 10
              failureThreshold: 3
              initialDelaySeconds: 10
            }
          ]
        }
      ]
      // SQLite + Azure Files is only safe with one writer. Pin to a single replica.
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
      volumes: [
        {
          name: 'data'
          storageType: 'AzureFile'
          storageName: acaStorage.name
        }
      ]
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
