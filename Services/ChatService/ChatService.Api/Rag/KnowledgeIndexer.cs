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

        private sealed record CatalogItem(int Id, string Name, string Category, string Description, decimal Price, string? Image);

        public async Task LoadCatalogAsync(CancellationToken cancellationToken)
        {
            var http = _httpFactory.CreateClient("catalog");
            var items = new List<CatalogItem>();

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

                    items.Add(new CatalogItem(id, name, category, description, price, image));
                }

                var totalPages = doc.RootElement.TryGetProperty("totalPages", out var tp) ? tp.GetInt32() : page;

                if (page >= totalPages)
                {
                    break;
                }
            }

            // Keep the previous snapshot when the catalog call returned nothing.
            if (items.Count == 0)
            {
                return;
            }

            var specifications = await LoadSpecificationsAsync(http, items, cancellationToken);
            var ratings = await LoadRatingsAsync(http, items, cancellationToken);

            var chunks = items.Select(item => new KnowledgeChunk
            {
                Id = $"product:{item.Id}",
                Kind = "product",
                Source = "catalog",
                Title = item.Name,
                Text = BuildProductText(
                    item.Category,
                    item.Description,
                    specifications.GetValueOrDefault(item.Id),
                    ratings.GetValueOrDefault(item.Id)),
                ProductId = item.Id,
                Price = item.Price,
                ImageUrl = item.Image,
                Category = item.Category
            }).ToList();

            await _index.ReplaceSourceAsync("catalog", chunks, cancellationToken);
            _logger.LogInformation(
                "Indexed {Count} catalog products ({Specs} with specifications, {Rated} with ratings).",
                chunks.Count, specifications.Count, ratings.Count);
        }

        public static string BuildProductText(
            string category,
            string description,
            string? specifications,
            (double Average, int Count)? rating)
        {
            var text = new StringBuilder($"Category: {category}. {description}");

            if (!string.IsNullOrWhiteSpace(specifications))
            {
                text.Append(" Specifications: ").Append(specifications);
            }

            if (rating is { Count: > 0 } r)
            {
                text.Append(" Customer rating: ")
                    .Append(r.Average.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture))
                    .Append(" out of 5 from ").Append(r.Count)
                    .Append(r.Count == 1 ? " review." : " reviews.");
            }

            return text.ToString();
        }

        // Specifications are only returned by the single-product endpoint, so fetch them a few at a time.
        private async Task<Dictionary<int, string>> LoadSpecificationsAsync(
            HttpClient http,
            List<CatalogItem> items,
            CancellationToken cancellationToken)
        {
            var result = new System.Collections.Concurrent.ConcurrentDictionary<int, string>();
            var baseUrl = _services.ProductServiceUrl.TrimEnd('/');

            await Parallel.ForEachAsync(
                items,
                new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = cancellationToken },
                async (item, token) =>
                {
                    try
                    {
                        using var response = await http.GetAsync($"{baseUrl}/api/Product/{item.Id}", token);

                        if (!response.IsSuccessStatusCode)
                        {
                            return;
                        }

                        using var doc = await JsonDocument.ParseAsync(
                            await response.Content.ReadAsStreamAsync(token), cancellationToken: token);

                        if (!doc.RootElement.TryGetProperty("specifications", out var list) ||
                            list.ValueKind != JsonValueKind.Array)
                        {
                            return;
                        }

                        var parts = new List<string>();

                        foreach (var spec in list.EnumerateArray())
                        {
                            var name = spec.TryGetProperty("name", out var n) ? n.GetString() : null;
                            var value = spec.TryGetProperty("value", out var v) ? v.GetString() : null;
                            var group = spec.TryGetProperty("group", out var g) ? g.GetString() : null;

                            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
                            {
                                continue;
                            }

                            parts.Add(string.IsNullOrWhiteSpace(group) ? $"{name}: {value}" : $"{group} {name}: {value}");
                        }

                        if (parts.Count > 0)
                        {
                            result[item.Id] = string.Join("; ", parts) + ".";
                        }
                    }
                    catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException && !token.IsCancellationRequested)
                    {
                        _logger.LogDebug(ex, "Specifications unavailable for product {ProductId}.", item.Id);
                    }
                });

            return new Dictionary<int, string>(result);
        }

        // Review service is optional: a failure just leaves products without ratings.
        private async Task<Dictionary<int, (double Average, int Count)>> LoadRatingsAsync(
            HttpClient http,
            List<CatalogItem> items,
            CancellationToken cancellationToken)
        {
            var result = new Dictionary<int, (double, int)>();
            var baseUrl = _services.ReviewServiceUrl.TrimEnd('/');

            // The summary endpoint accepts at most 50 ids per call.
            foreach (var batch in items.Select(i => i.Id).Chunk(50))
            {
                try
                {
                    using var response = await http.GetAsync(
                        $"{baseUrl}/api/Review/summary?productIds={string.Join(',', batch)}", cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        continue;
                    }

                    using var doc = await JsonDocument.ParseAsync(
                        await response.Content.ReadAsStreamAsync(cancellationToken),
                        cancellationToken: cancellationToken);

                    foreach (var summary in doc.RootElement.EnumerateArray())
                    {
                        var count = summary.GetProperty("count").GetInt32();

                        if (count > 0)
                        {
                            result[summary.GetProperty("productId").GetInt32()] =
                                (summary.GetProperty("average").GetDouble(), count);
                        }
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException or JsonException or KeyNotFoundException or InvalidOperationException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning(ex, "Ratings unavailable; indexing products without them.");
                    break;
                }
            }

            return result;
        }
    }
}
