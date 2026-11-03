// MailArchive — Azure Container Apps infrastructure (deploy into rg-emailarchive)
targetScope = 'resourceGroup'

@description('Azure region (same as the toolbox Postgres server).')
param location string = resourceGroup().location

@description('Short name used in resource names.')
param appName string = 'emailarchive'

@description('Deploy the container apps (false on the very first run, before secrets and images exist).')
param deployApps bool = true

@description('Full image reference for the API, e.g. <acr>.azurecr.io/mailarchive-api:<tag>')
param apiImage string = ''

@description('Full image reference for the web frontend, e.g. <acr>.azurecr.io/mailarchive-web:<tag>')
param webImage string = ''

@description('Custom hostname for the web app (empty = none). Bound with a managed certificate after DNS is in place.')
param customDomain string = ''

@description('Name of an existing managed certificate in the environment for customDomain (empty = not yet bound).')
param managedCertificateName string = ''

param jwtIssuer string = 'MailArchive'
param jwtAudience string = 'MailArchive'

@description('Azure Files quota in GiB for PST uploads + attachments.')
param fileShareQuotaGiB int = 1024

param tags object = {
  app: 'mailarchive'
  owner: 'tbx'
  managedBy: 'bicep'
}

var suffix = uniqueString(resourceGroup().id)
var acrName = 'acr${appName}${take(suffix, 6)}'
var storageName = 'st${appName}${take(suffix, 6)}'
var kvName = 'kv-${appName}-${take(suffix, 6)}'
var shareName = 'mailarchive-storage'
var apiAppName = 'archive-api'
var webAppName = 'archive'

// Built-in role definition IDs
var acrPullRoleId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'
var kvSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'id-${appName}'
  location: location
  tags: tags
}

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'log-${appName}'
  location: location
  tags: tags
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: acrName
  location: location
  tags: tags
  sku: { name: 'Basic' }
  properties: {
    adminUserEnabled: false
  }
}

resource acrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: acr
  name: guid(acr.id, identity.id, acrPullRoleId)
  properties: {
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
  }
}

resource kv 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: kvName
  location: location
  tags: tags
  properties: {
    tenantId: subscription().tenantId
    sku: { family: 'A', name: 'standard' }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 30
    enablePurgeProtection: true
    publicNetworkAccess: 'Enabled'
  }
}

resource kvSecretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: kv
  name: guid(kv.id, identity.id, kvSecretsUserRoleId)
  properties: {
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', kvSecretsUserRoleId)
  }
}

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageName
  location: location
  tags: tags
  kind: 'StorageV2'
  sku: { name: 'Standard_ZRS' }
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    supportsHttpsTrafficOnly: true
    allowSharedKeyAccess: true // required for SMB mounts in Container Apps
  }
}

resource fileService 'Microsoft.Storage/storageAccounts/fileServices@2023-05-01' = {
  parent: storage
  name: 'default'
  properties: {
    shareDeleteRetentionPolicy: { enabled: true, days: 14 }
  }
}

resource share 'Microsoft.Storage/storageAccounts/fileServices/shares@2023-05-01' = {
  parent: fileService
  name: shareName
  properties: {
    shareQuota: fileShareQuotaGiB
    enabledProtocols: 'SMB'
  }
}

resource env 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: 'cae-${appName}'
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
    workloadProfiles: [
      { name: 'Consumption', workloadProfileType: 'Consumption' }
    ]
  }
}

resource envStorage 'Microsoft.App/managedEnvironments/storages@2024-03-01' = {
  parent: env
  name: 'mailarchivefiles'
  properties: {
    azureFile: {
      accountName: storage.name
      accountKey: storage.listKeys().keys[0].value
      shareName: share.name
      accessMode: 'ReadWrite'
    }
  }
}

resource apiApp 'Microsoft.App/containerApps@2024-03-01' = if (deployApps) {
  name: apiAppName
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${identity.id}': {} }
  }
  dependsOn: [ acrPull, kvSecretsUser ]
  properties: {
    environmentId: env.id
    workloadProfileName: 'Consumption'
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: false // reachable only from inside the environment (via the web app)
        targetPort: 8080
        transport: 'http'
        allowInsecure: true
      }
      registries: [
        { server: acr.properties.loginServer, identity: identity.id }
      ]
      secrets: [
        { name: 'db-connection', keyVaultUrl: '${kv.properties.vaultUri}secrets/db-connection', identity: identity.id }
        { name: 'jwt-key', keyVaultUrl: '${kv.properties.vaultUri}secrets/jwt-key', identity: identity.id }
      ]
    }
    template: {
      containers: [
        {
          name: 'api'
          image: apiImage
          resources: { cpu: json('1.0'), memory: '2Gi' }
          env: [
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            { name: 'ASPNETCORE_URLS', value: 'http://+:8080' }
            { name: 'ConnectionStrings__DefaultConnection', secretRef: 'db-connection' }
            { name: 'Jwt__Key', secretRef: 'jwt-key' }
            { name: 'Jwt__Issuer', value: jwtIssuer }
            { name: 'Jwt__Audience', value: jwtAudience }
            { name: 'Jwt__ExpiresMinutes', value: '60' }
            { name: 'MailArchive__PstParserProvider', value: 'XstReader' }
            { name: 'MailArchive__MaxPageSize', value: '100' }
            { name: 'MailArchive__EnableAuditLogs', value: 'true' }
          ]
          volumeMounts: [
            { volumeName: 'storage', mountPath: '/app/storage' }
          ]
          probes: [
            {
              type: 'Startup'
              tcpSocket: { port: 8080 }
              initialDelaySeconds: 5
              periodSeconds: 10
              failureThreshold: 30 // EF migrations run on startup
            }
            {
              type: 'Liveness'
              tcpSocket: { port: 8080 }
              periodSeconds: 30
            }
          ]
        }
      ]
      // Imports run on an in-process background queue: keep exactly one replica, never scale to zero
      scale: { minReplicas: 1, maxReplicas: 1 }
      volumes: [
        { name: 'storage', storageType: 'AzureFile', storageName: envStorage.name }
      ]
    }
  }
}

resource webApp 'Microsoft.App/containerApps@2024-03-01' = if (deployApps) {
  name: webAppName
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: { '${identity.id}': {} }
  }
  dependsOn: [ acrPull ]
  properties: {
    environmentId: env.id
    workloadProfileName: 'Consumption'
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
        customDomains: empty(customDomain) ? [] : [
          empty(managedCertificateName) ? {
            name: customDomain
            bindingType: 'Disabled'
          } : {
            name: customDomain
            bindingType: 'SniEnabled'
            certificateId: resourceId('Microsoft.App/managedEnvironments/managedCertificates', env.name, managedCertificateName)
          }
        ]
      }
      registries: [
        { server: acr.properties.loginServer, identity: identity.id }
      ]
    }
    template: {
      containers: [
        {
          name: 'web'
          image: webImage
          resources: { cpu: json('0.25'), memory: '0.5Gi' }
          env: [
            { name: 'API_UPSTREAM', value: 'http://${apiAppName}' }
          ]
          probes: [
            { type: 'Liveness', httpGet: { path: '/healthz', port: 8080 }, periodSeconds: 30 }
            { type: 'Readiness', httpGet: { path: '/healthz', port: 8080 }, periodSeconds: 10 }
          ]
        }
      ]
      scale: { minReplicas: 1, maxReplicas: 3, rules: [
        { name: 'http', http: { metadata: { concurrentRequests: '50' } } }
      ] }
    }
  }
}

output acrName string = acr.name
output acrLoginServer string = acr.properties.loginServer
output keyVaultName string = kv.name
output environmentName string = env.name
output environmentStaticIp string = env.properties.staticIp
output environmentDefaultDomain string = env.properties.defaultDomain
output identityClientId string = identity.properties.clientId
output identityPrincipalId string = identity.properties.principalId
output webFqdn string = deployApps ? webApp!.properties.configuration.ingress.fqdn : ''
output customDomainVerificationId string = env.properties.customDomainConfiguration.customDomainVerificationId
