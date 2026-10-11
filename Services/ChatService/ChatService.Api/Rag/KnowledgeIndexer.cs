using System.Text;
using System.Text.Json;
using ChatService.Api.Options;
using Microsoft.Extensions.Options;

namespace ChatService.Api.Rag
{
    // Builds the knowledge index from the help documents and the live product catalog.
    public class KnowledgeIndexer : BackgroundService
    {
        private readonly KnowledgeIndex _index;
        private readonly IHttpClientFactory _httpFactory;
        private readonly ServiceUrlOptions _services;
        private readonly KnowledgeOptions _options;
        private readonly ILogger<KnowledgeIndexer> _logger;

        public KnowledgeIndexer(
            KnowledgeIndex index,
            IHttpClientFactory httpFactory,
            IOptions<ServiceUrlOptions> services,
            IOptions<KnowledgeOptions> options,
            ILogger<KnowledgeIndexer> logger)
        {
            _index = index;
            _httpFactory = httpFactory;
            _services = services.Value;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await SafeAsync(() => LoadDocumentsAsync(stoppingToken));

            var interval = TimeSpan.FromMinutes(Math.Max(1, _options.CatalogRefreshMinutes));

            while (!stoppingToken.IsCancellationRequested)
            {
                await SafeAsync(() => LoadCatalogAsync(stoppingToken));

                try
                {
                    await Task.Delay(interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        private async Task SafeAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Knowledge indexing step failed; will retry on the next cycle.");
            }
        }

        public async Task LoadDocumentsAsync(CancellationToken cancellationToken)
        {
            var folder = Path.Combine(AppContext.BaseDirectory, "Knowledge");

            if (!Directory.Exists(folder))
            {
                _logger.LogWarning("Knowledge folder {Folder} not found.", folder);
                return;
            }

            var chunks = new List<KnowledgeChunk>();

            foreach (var file in Directory.EnumerateFiles(folder, "*.md"))
            {
                chunks.AddRange(SplitMarkdown(
                    await File.ReadAllTextAsync(file, cancellationToken),
                    Path.GetFileNameWithoutExtension(file),
                    Path.GetFileName(file).StartsWith("admin-", StringComparison.OrdinalIgnoreCase) ? "admin-faq" : "faq"));
            }

            await _index.ReplaceSourceAsync("docs", chunks, cancellationToken);

            _logger.LogInformation("Indexed {Count} help chunks.", chunks.Count);
        }

        // One chunk per "## " heading; the "# " heading is the document section.
        public static List<KnowledgeChunk> SplitMarkdown(string markdown, string source, string kind = "faq")
        {
            var chunks = new List<KnowledgeChunk>();
            var section = string.Empty;
            string? title = null;
            var body = new StringBuilder();

            void Flush()
            {
                if (title is not null && body.Length > 0)
                {
                    chunks.Add(new KnowledgeChunk
                    {
                        Id = $"{source}:{chunks.Count}",
                        Kind = kind,
                        Source = source,
                        Title = title,
                        Text = $"{section}: {body.ToString().Trim()}"
                    });
                }

                body.Clear();
            }

            foreach (var line in markdown.Split('\n'))
            {
                if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    Flush();
                    title = line[3..].Trim();
                }
                else if (line.StartsWith("# ", StringComparison.Ordinal))
                {
                    Flush();
                    title = null;
                    section = line[2..].Trim();
                }
                else
                {
                    body.AppendLine(line.TrimEnd());
                }
            }

            Flush();

            return chunks;
        }

        public async Task LoadCatalogAsync(CancellationToken cancellationToken)
        {
            var http = _httpFactory.CreateClient("catalog");
            var chunks = new List<KnowledgeChunk>();

            for (var page = 1; page <= 50; page++)
            {
                var url = $"{_services.ProductServiceUrl.TrimEnd('/')}/api/Product/paged?pageNumber={page}&pageSize=100";

                using var response = await http.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();

                using var doc = await JsonDocument.ParseAsync(
                    await response.Content.ReadAsStreamAsync(cancellationToken),
                    cancellationToken: cancellationToken);

                foreach (var item in doc.RootElement.GetProperty("items").EnumerateArray())
                {
                    if (item.TryGetProperty("isActive", out var active) && active.ValueKind == JsonValueKind.False)
                    {
                        continue;
                    }

                    var id = item.GetProperty("id").GetInt32();
                    var name = item.GetProperty("name").GetString() ?? string.Empty;
                    var category = item.TryGetProperty("categoryName", out var c) ? c.GetString() ?? string.Empty : string.Empty;
                    var description = item.TryGetProperty("description", out var d) ? d.GetString() ?? string.Empty : string.Empty;
                    var price = item.GetProperty("price").GetDecimal();
                    var image = item.TryGetProperty("imageUrl", out var i) && i.ValueKind == JsonValueKind.String ? i.GetString() : null;

                    chunks.Add(new KnowledgeChunk
                    {
                        Id = $"product:{id}",
                        Kind = "product",
                        Source = "catalog",
                        Title = name,
                        Text = $"Category: {category}. {description}",
                        ProductId = id,
                        Price = price,
                        ImageUrl = image,
                        Category = category
                    });
                }

                var totalPages = doc.RootElement.TryGetProperty("totalPages", out var tp) ? tp.GetInt32() : page;

                if (page >= totalPages)
                {
                    break;
                }
            }

            // Keep the previous snapshot when the catalog call returned nothing.
            if (chunks.Count > 0)
            {
                await _index.ReplaceSourceAsync("catalog", chunks, cancellationToken);
                _logger.LogInformation("Indexed {Count} catalog products.", chunks.Count);
            }
        }
    }
}
