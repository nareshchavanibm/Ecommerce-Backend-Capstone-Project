namespace ChatService.Api.Options
{
    public class FoundryOptions
    {
        public const string Section = "Foundry";

        // Azure OpenAI / Foundry resource endpoint, e.g. https://my-resource.openai.azure.com
        public string Endpoint { get; set; } = string.Empty;

        public string ChatDeployment { get; set; } = "gpt-4o-mini";

        // Optional; without it the knowledge base falls back to keyword search.
        public string EmbeddingDeployment { get; set; } = string.Empty;

        public string ApiVersion { get; set; } = "2024-10-21";

        // Empty => Microsoft Entra ID (managed identity / developer sign-in) via DefaultAzureCredential.
        public string ApiKey { get; set; } = string.Empty;

        public int MaxOutputTokens { get; set; } = 500;

        public double Temperature { get; set; } = 0.2;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(Endpoint);

        public bool HasEmbeddings => IsConfigured && !string.IsNullOrWhiteSpace(EmbeddingDeployment);
    }

    public class ServiceUrlOptions
    {
        public const string Section = "Services";

        public string ProductServiceUrl { get; set; } = "http://productservice:8080";

        public string OrderServiceUrl { get; set; } = "http://orderservice:8080";
    }

    public class GovernanceOptions
    {
        public const string Section = "Governance";

        public int MaxMessageLength { get; set; } = 1000;

        public int MaxHistoryMessages { get; set; } = 10;

        public int MaxToolRounds { get; set; } = 4;

        public int RequestsPerMinute { get; set; } = 20;

        // Message text is never logged unless this is on (it is still PII-redacted).
        public bool LogRedactedContent { get; set; }

        public string SupportEmail { get; set; } = "support@verdant.example";
    }

    public class KnowledgeOptions
    {
        public const string Section = "Knowledge";

        public int CatalogRefreshMinutes { get; set; } = 30;

        public double MinVectorScore { get; set; } = 0.30;

        public double MinKeywordScore { get; set; } = 1.0;
    }
}
