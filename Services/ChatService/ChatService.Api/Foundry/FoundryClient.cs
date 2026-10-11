using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.Core;
using Azure.Identity;
using ChatService.Api.Options;
using Microsoft.Extensions.Options;

namespace ChatService.Api.Foundry
{
    public record ToolCall(string Id, string Name, string Arguments);

    public record ModelReply(
        string? Content,
        IReadOnlyList<ToolCall> ToolCalls,
        JsonObject AssistantMessage,
        bool Filtered,
        int PromptTokens,
        int CompletionTokens);

    public interface IChatModel
    {
        bool IsConfigured { get; }

        Task<ModelReply> CompleteAsync(
            IReadOnlyList<JsonObject> messages,
            JsonArray? tools,
            CancellationToken cancellationToken);
    }

    public interface IEmbeddingModel
    {
        bool IsConfigured { get; }

        Task<float[][]> EmbedAsync(
            IReadOnlyList<string> inputs,
            CancellationToken cancellationToken);
    }

    public class ModelUnavailableException : Exception
    {
        public ModelUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
    }

    // Talks to a model deployment in a Microsoft AI Foundry / Azure OpenAI resource.
    public class FoundryClient : IChatModel, IEmbeddingModel
    {
        private static readonly string[] Scope = { "https://cognitiveservices.azure.com/.default" };

        private readonly HttpClient _http;
        private readonly FoundryOptions _options;
        private readonly TokenCredential? _credential;

        public FoundryClient(HttpClient http, IOptions<FoundryOptions> options)
        {
            _http = http;
            _options = options.Value;

            if (_options.IsConfigured && string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                _credential = new DefaultAzureCredential();
            }
        }

        bool IChatModel.IsConfigured => _options.IsConfigured;

        bool IEmbeddingModel.IsConfigured => _options.HasEmbeddings;

        public async Task<ModelReply> CompleteAsync(
            IReadOnlyList<JsonObject> messages,
            JsonArray? tools,
            CancellationToken cancellationToken)
        {
            var body = new JsonObject
            {
                ["messages"] = new JsonArray(messages.Select(m => (JsonNode?)m.DeepClone()).ToArray()),
                ["max_tokens"] = _options.MaxOutputTokens,
                ["temperature"] = _options.Temperature
            };

            if (tools is { Count: > 0 })
            {
                body["tools"] = tools.DeepClone();
                body["tool_choice"] = "auto";
            }

            var path = $"openai/deployments/{Uri.EscapeDataString(_options.ChatDeployment)}/chat/completions";

            var (status, json) = await PostAsync(path, body, cancellationToken);

            if (status == HttpStatusCode.BadRequest &&
                json?["error"]?["code"]?.GetValue<string>() == "content_filter")
            {
                return new ModelReply(null, Array.Empty<ToolCall>(), new JsonObject(), true, 0, 0);
            }

            if (json?["choices"]?[0] is not JsonObject choice)
            {
                throw new ModelUnavailableException("The model returned an unexpected response.");
            }

            var message = (JsonObject)choice["message"]!.DeepClone();
            var finish = choice["finish_reason"]?.GetValue<string>();

            var calls = new List<ToolCall>();

            if (message["tool_calls"] is JsonArray toolCalls)
            {
                foreach (var call in toolCalls.OfType<JsonObject>())
                {
                    calls.Add(new ToolCall(
                        call["id"]?.GetValue<string>() ?? string.Empty,
                        call["function"]?["name"]?.GetValue<string>() ?? string.Empty,
                        call["function"]?["arguments"]?.GetValue<string>() ?? "{}"));
                }
            }

            return new ModelReply(
                message["content"]?.GetValue<string>(),
                calls,
                message,
                finish == "content_filter",
                json["usage"]?["prompt_tokens"]?.GetValue<int>() ?? 0,
                json["usage"]?["completion_tokens"]?.GetValue<int>() ?? 0);
        }

        public async Task<float[][]> EmbedAsync(
            IReadOnlyList<string> inputs,
            CancellationToken cancellationToken)
        {
            var result = new List<float[]>(inputs.Count);

            // Azure OpenAI caps the batch size, so send small batches.
            foreach (var batch in inputs.Chunk(16))
            {
                var body = new JsonObject
                {
                    ["input"] = new JsonArray(batch.Select(i => (JsonNode?)JsonValue.Create(i)).ToArray())
                };

                var path = $"openai/deployments/{Uri.EscapeDataString(_options.EmbeddingDeployment)}/embeddings";

                var (_, json) = await PostAsync(path, body, cancellationToken);

                var data = json?["data"] as JsonArray
                    ?? throw new ModelUnavailableException("The embedding model returned an unexpected response.");

                foreach (var item in data.OrderBy(d => d!["index"]!.GetValue<int>()))
                {
                    result.Add(item!["embedding"]!.AsArray().Select(v => v!.GetValue<float>()).ToArray());
                }
            }

            return result.ToArray();
        }

        private async Task<(HttpStatusCode Status, JsonNode? Json)> PostAsync(
            string path,
            JsonObject body,
            CancellationToken cancellationToken)
        {
            var url = $"{_options.Endpoint.TrimEnd('/')}/{path}?api-version={_options.ApiVersion}";

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
            };

            if (_credential is null)
            {
                request.Headers.Add("api-key", _options.ApiKey);
            }
            else
            {
                try
                {
                    var token = await _credential.GetTokenAsync(new TokenRequestContext(Scope), cancellationToken);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
                }
                catch (Exception ex) when (ex is CredentialUnavailableException or AuthenticationFailedException)
                {
                    throw new ModelUnavailableException("No Azure credentials are available for the model.", ex);
                }
            }

            HttpResponseMessage response;

            try
            {
                response = await _http.SendAsync(request, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                throw new ModelUnavailableException("The model could not be reached.", ex);
            }

            using (response)
            {
                var text = await response.Content.ReadAsStringAsync(cancellationToken);

                JsonNode? json = null;

                try
                {
                    json = JsonNode.Parse(text);
                }
                catch (JsonException)
                {
                }

                if (!response.IsSuccessStatusCode &&
                    !(response.StatusCode == HttpStatusCode.BadRequest &&
                      json?["error"]?["code"]?.GetValue<string>() == "content_filter"))
                {
                    throw new ModelUnavailableException(
                        $"The model call failed with status {(int)response.StatusCode}.");
                }

                return (response.StatusCode, json);
            }
        }
    }
}
