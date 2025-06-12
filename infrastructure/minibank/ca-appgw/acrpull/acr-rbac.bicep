targetScope = 'resourceGroup'

param containeruserPrincipalId string

resource acr 'Microsoft.ContainerRegistry/registries@2025-04-01' existing = {
  name: 'minibank'
}

@description('This is the built-in AcrPull role. See https://learn.microsoft.com/azure/role-based-access-control/built-in-roles#acrpull')
resource acrPullRoleDefinition 'Microsoft.Authorization/roleDefinitions@2022-04-01' existing = {
  scope: subscription()
  name: '7f951dda-4ed3-4680-a7ca-43fe172d538d'
}

resource acrRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: acr
  name: guid(acr.id, containeruserPrincipalId, 'AcrPull')
  properties: {
    roleDefinitionId: acrPullRoleDefinition.id
    principalId: containeruserPrincipalId
    principalType: 'ServicePrincipal'
  }
}
