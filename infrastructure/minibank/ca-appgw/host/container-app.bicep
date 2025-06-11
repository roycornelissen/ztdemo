@description('The location where the Backend API will be deployed to')
param location string

@description('The Container App environment that the Container App will be deployed to')
param containerAppEnvName string

@description('The tags that will be applied to the Backend API')
param tags object

@description('The name of the Container App')
param containerAppName string

@description('Specifies the docker container image to deploy.')
param containerImage string

@description('Specifies the container port.')
param targetPort int = 80

@description('The name of the kv secret that contains the client ID for the Azure AD application used to authenticate the Container App')
param clientSecretName string

@description('Name of the identity to assign to the Container App')
param identityName string

@description('ID of the i')
param pullIdentityId string

@description('Number of CPU cores the container can use. Can be with a maximum of two decimals.')
@allowed([
  '0.25'
  '0.5'
  '0.75'
  '1'
  '1.25'
  '1.5'
  '1.75'
  '2'
])
param cpuCore string = '0.5'

@description('Amount of memory (in gibibytes, GiB) allocated to the container up to 4GiB. Can be with a maximum of two decimals. Ratio with CPU cores must be equal to 2.')
@allowed([
  '0.5'
  '1'
  '1.5'
  '2'
  '3'
  '3.5'
  '4'
])
param memorySize string = '1'

@description('Minimum number of replicas that will be deployed')
@minValue(0)
@maxValue(25)
param minReplicas int = 1

@description('Maximum number of replicas that will be deployed')
@minValue(0)
@maxValue(25)
param maxReplicas int = 3

resource kv 'Microsoft.KeyVault/vaults@2024-12-01-preview' existing = {
  name: 'kv-minibank'
}

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2025-01-31-preview' existing = {
  name: identityName
}

resource env 'Microsoft.App/managedEnvironments@2023-11-02-preview' existing = {
  name: containerAppEnvName
}

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: containerAppName
  tags: tags
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: env.id
    configuration: {
      ingress: {
        external: true
        targetPort: targetPort
        allowInsecure: false
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
      }
      secrets: [
        {
          name: 'clientsecret'
          keyVaultUrl: '${kv.properties.vaultUri}/secrets/${clientSecretName}'
          identity: identity.id
        }        
      ]
      registries: [
        {
          server: 'minibank.azurecr.io'
          identity: pullIdentityId
        }        
      ]
    }
    template: {
      revisionSuffix: 'firstrevision'
      containers: [
        {
          name: containerAppName
          image: containerImage
          resources: {
            cpu: json(cpuCore)
            memory: '${memorySize}Gi'
          }
          env: [
            {
              name: 'Entra__ClientSecret'
              secretRef: 'clientsecret'
            }
            {
              name: 'AzureStorage__QueueEndpoint'
              value: 'https://stminibank.queue.core.windows.net/'
            }
            {
              name: 'AzureStorage__TableEndpoint'
              value: 'https://stminibank.table.core.windows.net/'
            }
          ]
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
      }
    }
  }
}

@description('The FQDN of the Container App')
output fqdn string = containerApp.properties.configuration.ingress.fqdn
