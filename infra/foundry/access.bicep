// Grants the chat service's managed identity permission to call the model deployments (use with disableLocalAuth = true).
targetScope = 'resourceGroup'

param foundryAccountName string

@description('Object (principal) id of the managed identity the chat service runs as.')
param principalId string

// Cognitive Services OpenAI User
var roleId = '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'

resource foundry 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' existing = {
  name: foundryAccountName
}

resource assignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: foundry
  name: guid(foundry.id, principalId, roleId)
  properties: {
    principalId: principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleId)
  }
}
