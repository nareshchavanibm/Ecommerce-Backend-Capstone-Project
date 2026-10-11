# Microsoft AI Foundry setup

Provisions the model backend for the store assistant (`Services/ChatService`).

## What gets created
- Foundry resource (`AIServices`) and a project, `<prefix>-support-assistant`
- Deployments: `gpt-4o-mini` (agents) and `text-embedding-3-small` (RAG embeddings)
- Content filter policy `store-assistant-filter` (hate, sexual, violence, self-harm, jailbreak and indirect-attack prompt shields, protected material)
- Log Analytics workspace with Audit and RequestResponse logs

## Deploy
```powershell
az login
./deploy.ps1 -ResourceGroup rg-store-ai -NamePrefix mystore
```
Put the printed values in the environment used by `docker-compose.yml`:

| Variable | Value |
| --- | --- |
| `FOUNDRY_ENDPOINT` | printed endpoint |
| `FOUNDRY_CHAT_DEPLOYMENT` | `gpt-4o-mini` |
| `FOUNDRY_EMBEDDING_DEPLOYMENT` | `text-embedding-3-small` (empty = keyword search only) |
| `FOUNDRY_API_KEY` | from `az cognitiveservices account keys list` (dev only) |

Then `podman compose up -d --build chatservice apigateway`. Without `FOUNDRY_ENDPOINT` the assistant still answers from retrieval only.

## Explore the model catalog
1. Open https://ai.azure.com and select the project.
2. **Model catalog**: compare models by capability, price and region. Check "supports tool calling" and "supports embeddings".
3. To try another chat model, deploy it from the catalog and set `FOUNDRY_CHAT_DEPLOYMENT`. The service only needs a chat-completions deployment with tool calling.
4. Use the **Playground** with the system prompts in `Services/ChatService/ChatService.Api/Agents/Agents.cs` to compare models before switching.
5. Under **Evaluation**, run quality and safety evaluations on a set of sample questions (order status, FAQ, recommendations, injection attempts) before each model change.

## Production: managed identity instead of keys
1. Redeploy with `disableLocalAuth=true`.
2. Leave `FOUNDRY_API_KEY` empty; the service then uses `DefaultAzureCredential`.
3. Grant the service's identity access:
```powershell
az deployment group create -g rg-store-ai --template-file access.bicep `
  --parameters foundryAccountName=mystore-foundry principalId=<managed-identity-object-id>
```
