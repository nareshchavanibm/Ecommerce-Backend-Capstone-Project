// Microsoft AI Foundry resource, project, model deployments, content filter policy and monitoring for the store assistant.
targetScope = 'resourceGroup'

@description('Globally unique name prefix, 2-20 lowercase letters/digits.')
@minLength(2)
@maxLength(20)
param namePrefix string

param location string = resourceGroup().location

@description('Chat model deployed for the agents. Check the Foundry model catalog for what is available in your region.')
param chatModelName string = 'gpt-4o-mini'
param chatModelVersion string = '2024-07-18'

param embeddingModelName string = 'text-embedding-3-small'
param embeddingModelVersion string = '1'

@description('Tokens-per-minute capacity in thousands for each deployment.')
param capacity int = 30

@description('Set true in production so only Microsoft Entra ID (managed identity) can call the models.')
param disableLocalAuth bool = false

var accountName = '${namePrefix}-foundry'
var projectName = '${namePrefix}-support-assistant'
var filterName = 'store-assistant-filter'

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${namePrefix}-logs'
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource foundry 'Microsoft.CognitiveServices/accounts@2025-04-01-preview' = {
  name: accountName
  location: location
  kind: 'AIServices'
  sku: { name: 'S0' }
  identity: { type: 'SystemAssigned' }
  properties: {
    customSubDomainName: accountName
    allowProjectManagement: true
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: disableLocalAuth
  }
}

resource project 'Microsoft.CognitiveServices/accounts/projects@2025-04-01-preview' = {
  parent: foundry
  name: projectName
  location: location
  identity: { type: 'SystemAssigned' }
  properties: {
    displayName: 'Store support assistant'
    description: 'Customer service and product recommendation agents.'
  }
}

// Responsible AI: block harmful content and prompt attacks on input, harmful and copyrighted content on output.
resource contentFilter 'Microsoft.CognitiveServices/accounts/raiPolicies@2025-04-01-preview' = {
  parent: foundry
  name: filterName
  properties: {
    basePolicyName: 'Microsoft.DefaultV2'
    mode: 'Blocking'
    contentFilters: [
      { name: 'Hate', severityThreshold: 'Low', blocking: true, enabled: true, source: 'Prompt' }
      { name: 'Hate', severityThreshold: 'Low', blocking: true, enabled: true, source: 'Completion' }
      { name: 'Sexual', severityThreshold: 'Low', blocking: true, enabled: true, source: 'Prompt' }
      { name: 'Sexual', severityThreshold: 'Low', blocking: true, enabled: true, source: 'Completion' }
      { name: 'Violence', severityThreshold: 'Low', blocking: true, enabled: true, source: 'Prompt' }
      { name: 'Violence', severityThreshold: 'Low', blocking: true, enabled: true, source: 'Completion' }
      { name: 'Selfharm', severityThreshold: 'Low', blocking: true, enabled: true, source: 'Prompt' }
      { name: 'Selfharm', severityThreshold: 'Low', blocking: true, enabled: true, source: 'Completion' }
      { name: 'Jailbreak', blocking: true, enabled: true, source: 'Prompt' }
      { name: 'Indirect Attack', blocking: true, enabled: true, source: 'Prompt' }
      { name: 'Protected Material Text', blocking: true, enabled: true, source: 'Completion' }
    ]
  }
}

resource chat 'Microsoft.CognitiveServices/accounts/deployments@2025-04-01-preview' = {
  parent: foundry
  name: chatModelName
  sku: { name: 'GlobalStandard', capacity: capacity }
  properties: {
    model: { format: 'OpenAI', name: chatModelName, version: chatModelVersion }
    raiPolicyName: contentFilter.name
  }
}

resource embeddings 'Microsoft.CognitiveServices/accounts/deployments@2025-04-01-preview' = {
  parent: foundry
  name: embeddingModelName
  // Deployments on one account are created one at a time.
  dependsOn: [ chat ]
  sku: { name: 'GlobalStandard', capacity: capacity }
  properties: {
    model: { format: 'OpenAI', name: embeddingModelName, version: embeddingModelVersion }
    raiPolicyName: 'Microsoft.DefaultV2'
  }
}

// Request/response audit trail for model calls.
resource diagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'to-log-analytics'
  scope: foundry
  properties: {
    workspaceId: logs.id
    logs: [
      { category: 'Audit', enabled: true }
      { category: 'RequestResponse', enabled: true }
    ]
    metrics: [
      { category: 'AllMetrics', enabled: true }
    ]
  }
}

output endpoint string = 'https://${foundry.properties.customSubDomainName}.openai.azure.com'
output foundryAccount string = foundry.name
output chatDeployment string = chat.name
output embeddingDeployment string = embeddings.name
output projectName string = project.name
