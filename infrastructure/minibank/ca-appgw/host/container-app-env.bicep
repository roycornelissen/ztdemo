param envName string
param location string
param lawName string
param acaSubnetId string
param tags object
param pullIdentityId string

resource law 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: lawName
}

resource env 'Microsoft.App/managedEnvironments@2025-02-02-preview' = {
  name: envName
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${pullIdentityId}': {}
    }
  }
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: law.properties.customerId
        sharedKey: law.listKeys().primarySharedKey
      }
    }
    zoneRedundant: false
    vnetConfiguration: {
      infrastructureSubnetId: acaSubnetId
      internal: true
    }
  }
}

output containerAppEnvName string = env.name
output domain string = env.properties.defaultDomain
output staticIp string = env.properties.staticIp

