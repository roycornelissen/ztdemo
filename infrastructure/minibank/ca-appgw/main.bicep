@description('The suffix applied to all resources')
param appSuffix string = 'minibank'

@description('The location to deploy all these resources to')
param location string = resourceGroup().location

@description('The tags to apply to all resources')
param tags object = {
  Application: 'Zero-Trust Minibank Payments'
  Environment: 'DEMO'
  Owner: 'Roy Cornelissen'
  Purpose: 'DevSum demo'
}

@description('The name of the container app env')
param envName string = 'env-${appSuffix}-1'

@description('The name of the Virtual Network that will be deployed')
param virtualNetworkName string = 'vnet-payments'

@description('The name of the Log Analytics workspace that will be deployed')
param logAnalyticsName string = 'law-${appSuffix}'

@description('The name of the App Gateway that will be deployed')
param appGatewayName string = 'agw-${appSuffix}'

@description('The name of the Public IP address that will be deployed')
param ipAddressName string = 'pip-${appGatewayName}'

@description('The tag of the images to deploy')
param imageTag string = 'latest'

@description('The revision suffix to apply to the Container Apps. This is used to create a new revision of the Container Apps when deploying updates.')
param revisionSuffix string = uniqueString(resourceGroup().id)

@description('This is the built-in Contributor role. See https://learn.microsoft.com/azure/role-based-access-control/built-in-roles#contributor')
resource networkContributorRoleDefinition 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  scope: subscription()
  name: '4d97b98b-1d4f-4787-a291-c67834d212e7'
}

resource roleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, deployer().objectId, networkContributorRoleDefinition.id)
  properties: {
    roleDefinitionId: networkContributorRoleDefinition.id
    principalId: deployer().objectId
    principalType: 'User'
  }
}

resource containeruser 'Microsoft.ManagedIdentity/userAssignedIdentities@2025-01-31-preview' = {
  name: 'id-containeruser-${appSuffix}'
  location: location
  tags: tags
}

resource vnet 'Microsoft.Network/virtualNetworks@2024-07-01' existing = {
  name: virtualNetworkName
}

resource acaSubnet 'Microsoft.Network/virtualNetworks/subnets@2024-07-01' existing = {
  parent: vnet
  name: 'snet-apps'
}

resource appGatewaySubnet 'Microsoft.Network/virtualNetworks/subnets@2024-07-01' existing = {
  parent: vnet
  name: 'snet-appgw'
}

module registry 'acrpull/acr-rbac.bicep' = {
  scope: resourceGroup('rg-minibank-dev')
  name: 'acr'
  params: {
    containeruserPrincipalId: containeruser.properties.principalId
  }
}

module law 'monitoring/log-analytics.bicep' = {
  name: 'law'
  params: {
    location: location 
    logAnalyticsWorkspaceName: logAnalyticsName
    tags: tags
  }
}

module env 'host/container-app-env.bicep' = {
  name: 'env'
  dependsOn: [
    registry
  ]
  params: {
    acaSubnetId: acaSubnet.id 
    envName: envName 
    lawName: law.outputs.name
    location: location
    tags: tags
    pullIdentityId: containeruser.id
  }
}

module accountapi 'host/container-app.bicep' = {
  name: 'accounts'
  dependsOn: [
    registry
  ]
  params: {
    containerAppEnvName: env.outputs.containerAppEnvName
    containerAppName: 'ca-accountapi-${appSuffix}'
    containerImage: 'minibank.azurecr.io/minibank/accounts:${imageTag}'
    location: location
    tags: tags
    identityName: 'id-accounts-api'
    pullIdentityId: containeruser.id
    targetPort: 8080
    clientSecretName: 'accounts-client-secret'
    revisionSuffix: revisionSuffix
  }
}

module paymentapi 'host/container-app.bicep' = {
  name: 'payments'
  params: {
    containerAppEnvName: env.outputs.containerAppEnvName
    containerAppName: 'ca-payments'
    containerImage: 'minibank.azurecr.io/minibank/payments:${imageTag}'
    location: location
    tags: tags
    identityName: 'id-payments-api'
    pullIdentityId: containeruser.id
    targetPort: 8080
    clientSecretName: 'payments-client-secret'
    revisionSuffix: revisionSuffix
  }
}

module processing 'host/container-app.bicep' = {
  name: 'processing'
  params: {
    containerAppEnvName: env.outputs.containerAppEnvName
    containerAppName: 'ca-processing'
    containerImage: 'minibank.azurecr.io/minibank/processing:${imageTag}'
    location: location
    tags: tags
    identityName: 'id-processing'
    pullIdentityId: containeruser.id
    clientSecretName: 'payments-client-secret'
    revisionSuffix: revisionSuffix
  }
}

module privateDnsZone 'network/private-dns-zone.bicep' = {
  name: 'pdns'
  params: {
    domain: env.outputs.domain
    envStaticIp: env.outputs.staticIp
    tags: tags
    vnetName: vnet.name
  }
}

module appGateway 'network/app-gateway.bicep' = {
  name: 'appgateway'
  params: {
    appGatewayName: appGatewayName
    pool1_fqdn: paymentapi.outputs.fqdn
    pool1_path: '/payment'
    pool2_fqdn: accountapi.outputs.fqdn
    pool2_path: '/accounts'
    ipAddressName: ipAddressName
    location: location
    subnetId: appGatewaySubnet.id
    tags: tags
  }
}
