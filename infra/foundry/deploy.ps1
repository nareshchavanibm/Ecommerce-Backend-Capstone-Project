param(
    [Parameter(Mandatory)] [string] $ResourceGroup,
    [Parameter(Mandatory)] [string] $NamePrefix,
    [string] $Location = 'eastus2'
)

$ErrorActionPreference = 'Stop'

az group create --name $ResourceGroup --location $Location | Out-Null

$result = az deployment group create `
    --resource-group $ResourceGroup `
    --template-file "$PSScriptRoot/main.bicep" `
    --parameters namePrefix=$NamePrefix `
    --query properties.outputs -o json | ConvertFrom-Json

$account = $result.foundryAccount.value

Write-Host "FOUNDRY_ENDPOINT=$($result.endpoint.value)"
Write-Host "FOUNDRY_CHAT_DEPLOYMENT=$($result.chatDeployment.value)"
Write-Host "FOUNDRY_EMBEDDING_DEPLOYMENT=$($result.embeddingDeployment.value)"
Write-Host ""
Write-Host "Set FOUNDRY_API_KEY from: az cognitiveservices account keys list -g $ResourceGroup -n $account --query key1 -o tsv"
Write-Host "Do not commit the key. Production should use managed identity (see README)."
