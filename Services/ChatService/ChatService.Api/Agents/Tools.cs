using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ChatService.Api.Models;
using ChatService.Api.Options;
using ChatService.Api.Rag;
using Microsoft.Extensions.Options;

namespace ChatService.Api.Agents
{
    // Per-request state shared by the tools of one agent run.
    public class AgentContext
    {
        public ChatRole Role { get; init; }

        public string? UserId { get; init; }

        public string? BearerToken { get; init; }

        public List<Citation> Citations { get; } = new();

        public List<ProductCard> Products { get; } = new();

        public OrderCard? Order { get; set; }

        public List<OrderCard> Orders { get; } = new();

        public bool Escalate { get; set; }

        public List<string> ToolsUsed { get; } = new();
    }

    public interface IAgentTool
    {
        string Name { get; }

        // Roles allowed to use the tool; enforced by the runner and again inside admin tools.
        IReadOnlyCollection<ChatRole> Roles { get; }

        // OpenAI "function" tool definition.
        JsonObject Definition { get; }

        Task<string> ExecuteAsync(JsonObject arguments, AgentContext context, CancellationToken cancellationToken);
    }

    internal static class ToolRoles
    {
        public static readonly ChatRole[] Everyone = { ChatRole.Anonymous, ChatRole.Customer, ChatRole.Admin };

        public static readonly ChatRole[] Shoppers = { ChatRole.Anonymous, ChatRole.Customer };

        public static readonly ChatRole[] AdminOnly = { ChatRole.Admin };
    }

    internal static class ToolSchema
    {
        public static JsonObject Function(string name, string description, JsonObject properties, params string[] required) =>
            new()
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = name,
                    ["description"] = description,
                    ["parameters"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = properties,
                        ["required"] = new JsonArray(required.Select(r => (JsonNode?)JsonValue.Create(r)).ToArray())
                    }
                }
            };

        public static JsonObject String(string description) =>
            new() { ["type"] = "string", ["description"] = description };

        public static JsonObject Number(string description) =>
            new() { ["type"] = "number", ["description"] = description };

        public static string Error(string code) => new JsonObject { ["error"] = code }.ToJsonString();
    }

    public partial class OrderLookupClient
    {
        [GeneratedRegex(@"^ORD-[A-Z0-9]{6,12}$")]
        public static partial Regex OrderNumberPattern();

        private readonly HttpClient _http;
        private readonly ServiceUrlOptions _services;

        public OrderLookupClient(HttpClient http, IOptions<ServiceUrlOptions> services)
        {
            _http = http;
            _services = services.Value;
        }

        // The caller's own token is forwarded, so OrderService enforces that orders belong to the signed-in customer.
        public async Task<JsonElement?> GetAsync(string path, AgentContext context, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{_services.OrderServiceUrl.TrimEnd('/')}/api/Order/{path}");

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.BearerToken);

            using var response = await _http.SendAsync(request, cancellationToken);

            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            using var doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);

            return doc.RootElement.Clone();
        }

        public const int MaxListed = 20;

        // Accepts any casing and the US spelling of Cancelled; blank means no filter.
        public static string? NormalizeStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return null;
            }

            var value = status.Trim().ToLowerInvariant().Replace("canceled", "cancelled");

            return char.ToUpperInvariant(value[0]) + value[1..];
        }

        public static int ReadLimit(JsonObject arguments, int fallback)
        {
            if (arguments["limit"] is not JsonValue v)
            {
                return fallback;
            }

            if (v.TryGetValue<int>(out var whole))
            {
                return Math.Clamp(whole, 1, MaxListed);
            }

            return v.TryGetValue<decimal>(out var number) ? Math.Clamp((int)number, 1, MaxListed) : fallback;
        }

        public static OrderCard ToCard(JsonElement order) => new()
        {
            OrderNumber = order.GetProperty("orderNumber").GetString() ?? string.Empty,
            Status = order.GetProperty("status").GetString() ?? string.Empty,
            TotalAmount = order.GetProperty("totalAmount").GetDecimal(),
            OrderDate = order.GetProperty("orderDate").GetDateTime(),
            Items = order.GetProperty("items").EnumerateArray()
                .Select(i => $"{i.GetProperty("quantity").GetInt32()} x {i.GetProperty("productName").GetString()}")
                .ToList()
        };
    }

    public class LookupOrderTool : IAgentTool
    {
        private readonly OrderLookupClient _orders;

        public LookupOrderTool(OrderLookupClient orders) => _orders = orders;

        public string Name => "lookup_order";

        public IReadOnlyCollection<ChatRole> Roles => ToolRoles.Everyone;

        public JsonObject Definition => ToolSchema.Function(
            Name,
            "Get the status and items of one of the signed-in customer's orders by its order number.",
            new JsonObject { ["order_number"] = ToolSchema.String("Order number like ORD-1A2B3C4D.") },
            "order_number");

        public async Task<string> ExecuteAsync(JsonObject arguments, AgentContext context, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(context.BearerToken))
            {
                return ToolSchema.Error("login_required");
            }

            var number = (arguments["order_number"]?.GetValue<string>() ?? string.Empty).Trim().ToUpperInvariant();

            if (!OrderLookupClient.OrderNumberPattern().IsMatch(number))
            {
                return ToolSchema.Error("invalid_order_number");
            }

            var order = await _orders.GetAsync(number, context, cancellationToken);

            if (order is null)
            {
                return ToolSchema.Error("order_not_found_for_this_account");
            }

            context.Order = OrderLookupClient.ToCard(order.Value);

            return JsonSerializer.Serialize(context.Order);
        }
    }

    public class ListMyOrdersTool : IAgentTool
    {
        private readonly OrderLookupClient _orders;

        public ListMyOrdersTool(OrderLookupClient orders) => _orders = orders;

        public string Name => "list_my_orders";

        public IReadOnlyCollection<ChatRole> Roles => ToolRoles.Shoppers;

        public JsonObject Definition => ToolSchema.Function(
            Name,
            "List the signed-in customer's orders, newest first, optionally only those with one status (for example 'list my delivered orders'). Returns the total number of matches and up to 'limit' orders.",
            new JsonObject
            {
                ["status"] = ToolSchema.String("Optional status filter: Pending, Confirmed, Processing, Shipped, Delivered or Cancelled."),
                ["limit"] = ToolSchema.Number("How many orders to list, 1 to 20. Default 10.")
            });

        public async Task<string> ExecuteAsync(JsonObject arguments, AgentContext context, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(context.BearerToken))
            {
                return ToolSchema.Error("login_required");
            }

            var orders = await _orders.GetAsync("my-orders", context, cancellationToken);

            if (orders is null || orders.Value.ValueKind != JsonValueKind.Array)
            {
                return JsonSerializer.Serialize(new { totalMatches = 0, orders = Array.Empty<OrderCard>() });
            }

            var status = OrderLookupClient.NormalizeStatus(arguments["status"]?.ToString());

            var matching = orders.Value.EnumerateArray()
                .Select(OrderLookupClient.ToCard)
                .Where(o => status is null || string.Equals(o.Status, status, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(o => o.OrderDate)
                .ToList();

            var cards = matching.Take(OrderLookupClient.ReadLimit(arguments, 10)).ToList();

            context.Orders.Clear();
            context.Orders.AddRange(cards);

            return JsonSerializer.Serialize(new { totalMatches = matching.Count, orders = cards });
        }
    }

    public class SearchKnowledgeTool : IAgentTool
    {
        private readonly KnowledgeIndex _index;

        public SearchKnowledgeTool(KnowledgeIndex index) => _index = index;

        public string Name => "search_knowledge";

        public IReadOnlyCollection<ChatRole> Roles => ToolRoles.Shoppers;

        public JsonObject Definition => ToolSchema.Function(
            Name,
            "Search the store's help articles (orders, checkout, payment, account, policies). Returns passages to ground the answer.",
            new JsonObject { ["query"] = ToolSchema.String("The customer's question in a few words.") },
            "query");

        public async Task<string> ExecuteAsync(JsonObject arguments, AgentContext context, CancellationToken cancellationToken)
        {
            var query = arguments["query"]?.GetValue<string>() ?? string.Empty;

            var hits = await _index.SearchAsync(query, "faq", 3, cancellationToken);

            foreach (var hit in hits)
            {
                if (!context.Citations.Any(c => c.Title == hit.Chunk.Title))
                {
                    context.Citations.Add(new Citation { Title = hit.Chunk.Title, Source = hit.Chunk.Source });
                }
            }

            if (hits.Count == 0)
            {
                return ToolSchema.Error("no_relevant_article_found");
            }

            return JsonSerializer.Serialize(hits.Select(h => new { title = h.Chunk.Title, text = h.Chunk.Text }));
        }
    }

    public class EscalateTool : IAgentTool
    {
        private readonly GovernanceOptions _options;

        public EscalateTool(IOptions<GovernanceOptions> options) => _options = options.Value;

        public string Name => "escalate_to_human";

        public IReadOnlyCollection<ChatRole> Roles => ToolRoles.Shoppers;

        public JsonObject Definition => ToolSchema.Function(
            Name,
            "Hand the customer to a human agent. Use when they ask for a person, are upset, or the issue is outside what you can do (refunds, damaged items, delivery problems, account problems).",
            new JsonObject { ["reason"] = ToolSchema.String("Short reason.") },
            "reason");

        public Task<string> ExecuteAsync(JsonObject arguments, AgentContext context, CancellationToken cancellationToken)
        {
            context.Escalate = true;

            return Task.FromResult(new JsonObject
            {
                ["status"] = "handoff_offered",
                ["support_email"] = _options.SupportEmail
            }.ToJsonString());
        }
    }

    public class SearchProductsTool : IAgentTool
    {
        private readonly KnowledgeIndex _index;

        public SearchProductsTool(KnowledgeIndex index) => _index = index;

        public string Name => "search_products";

        public IReadOnlyCollection<ChatRole> Roles => ToolRoles.Shoppers;

        public JsonObject Definition => ToolSchema.Function(
            Name,
            "Search the live product catalog. Returns real products with prices in INR.",
            new JsonObject
            {
                ["query"] = ToolSchema.String("What the customer wants, e.g. 'running shoes' or 'gift for a gardener'."),
                ["max_price"] = ToolSchema.Number("Optional maximum price in INR."),
                ["min_price"] = ToolSchema.Number("Optional minimum price in INR."),
                ["category"] = ToolSchema.String("Optional category name.")
            },
            "query");

        public async Task<string> ExecuteAsync(JsonObject arguments, AgentContext context, CancellationToken cancellationToken)
        {
            var query = arguments["query"]?.GetValue<string>() ?? string.Empty;
            var max = ReadNumber(arguments, "max_price");
            var min = ReadNumber(arguments, "min_price");
            var category = arguments["category"]?.GetValue<string>();

            bool Filter(KnowledgeChunk c) =>
                (max is null || c.Price <= max) &&
                (min is null || c.Price >= min) &&
                (string.IsNullOrWhiteSpace(category) ||
                 string.Equals(c.Category, category, StringComparison.OrdinalIgnoreCase));

            var hits = await _index.SearchAsync(query, "product", 4, cancellationToken, Filter);

            if (hits.Count == 0 && !string.IsNullOrWhiteSpace(category))
            {
                // The model often guesses the category name; retry on the query text alone.
                hits = await _index.SearchAsync(query, "product", 4, cancellationToken,
                    c => (max is null || c.Price <= max) && (min is null || c.Price >= min));
            }

            context.Products.Clear();

            foreach (var hit in hits)
            {
                context.Products.Add(new ProductCard
                {
                    Id = hit.Chunk.ProductId ?? 0,
                    Name = hit.Chunk.Title,
                    Price = hit.Chunk.Price ?? 0,
                    ImageUrl = hit.Chunk.ImageUrl,
                    Category = hit.Chunk.Category ?? string.Empty
                });
            }

            if (hits.Count == 0)
            {
                return ToolSchema.Error("no_matching_products");
            }

            return JsonSerializer.Serialize(hits.Select(h => new
            {
                id = h.Chunk.ProductId,
                name = h.Chunk.Title,
                category = h.Chunk.Category,
                price_inr = h.Chunk.Price,
                description = h.Chunk.Text
            }));
        }

        private static decimal? ReadNumber(JsonObject arguments, string name)
        {
            return arguments[name] is JsonValue value && value.TryGetValue<decimal>(out var number) ? number : null;
        }
    }

    public class ListProductsTool : IAgentTool
    {
        public const int MaxListed = 30;

        // Words that describe the request rather than the product.
        private static readonly HashSet<string> RequestWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "list", "show", "display", "give", "find", "search", "please", "product", "item", "related", "relate",
            "relating", "available", "category", "categorie", "store", "all", "every", "under", "below", "above",
            "range", "price", "tell", "let", "us", "like", "type", "kind", "sort", "latest", "new", "stock"
        };

        private readonly KnowledgeIndex _index;

        public ListProductsTool(KnowledgeIndex index) => _index = index;

        public string Name => "list_products";

        public IReadOnlyCollection<ChatRole> Roles => ToolRoles.Everyone;

        public JsonObject Definition => ToolSchema.Function(
            Name,
            "List every catalog product that matches a product type, keyword or category (for example 'mobile', 'laptops'). Use when the customer asks to list or show products; returns the total number of matches and up to 'limit' products, cheapest first.",
            new JsonObject
            {
                ["query"] = ToolSchema.String("Product type or keyword, e.g. 'mobile'. Leave empty to list the whole catalog."),
                ["category"] = ToolSchema.String("Optional category name."),
                ["min_price"] = ToolSchema.Number("Optional minimum price in INR."),
                ["max_price"] = ToolSchema.Number("Optional maximum price in INR."),
                ["limit"] = ToolSchema.Number($"How many products to return, 1 to {MaxListed}. Default 20.")
            });

        public Task<string> ExecuteAsync(JsonObject arguments, AgentContext context, CancellationToken cancellationToken)
        {
            var terms = ListingTerms($"{arguments["query"]?.ToString()} {arguments["category"]?.ToString()}");
            var min = ReadNumber(arguments, "min_price");
            var max = ReadNumber(arguments, "max_price");
            var limit = arguments["limit"] is JsonValue v && v.TryGetValue<decimal>(out var l) ? Math.Clamp((int)l, 1, MaxListed) : 20;

            var candidates = _index.Chunks("product")
                .Where(c => (min is null || c.Price >= min) && (max is null || c.Price <= max))
                .ToList();

            var matches = terms.Length == 0 ? candidates : Match(candidates, terms);

            var listed = matches.OrderBy(c => c.Price).ThenBy(c => c.Title).Take(limit).ToList();

            context.Products.Clear();

            foreach (var chunk in listed)
            {
                context.Products.Add(new ProductCard
                {
                    Id = chunk.ProductId ?? 0,
                    Name = chunk.Title,
                    Price = chunk.Price ?? 0,
                    ImageUrl = chunk.ImageUrl,
                    Category = chunk.Category ?? string.Empty
                });
            }

            if (matches.Count == 0)
            {
                return Task.FromResult(ToolSchema.Error("no_matching_products"));
            }

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                totalMatches = matches.Count,
                products = listed.Select(c => new { id = c.ProductId, name = c.Title, category = c.Category, price_inr = c.Price })
            }));
        }

        public static string[] ListingTerms(string text) =>
            KnowledgeIndex.Tokenize(text).Where(t => !RequestWords.Contains(t) && !decimal.TryParse(t, out _)).Distinct().ToArray();

        // Name and category matches win over passing mentions in a description.
        private static List<KnowledgeChunk> Match(List<KnowledgeChunk> candidates, string[] terms)
        {
            var named = candidates
                .Where(c => ContainsAll(KnowledgeIndex.Tokenize($"{c.Title} {c.Category}"), terms))
                .ToList();

            if (named.Count > 0)
            {
                return named;
            }

            var described = candidates.Where(c => ContainsAll(c.TokenSet(), terms)).ToList();

            if (described.Count > 0)
            {
                return described;
            }

            return candidates.Where(c => terms.Any(t => c.TokenSet().Contains(t))).ToList();
        }

        private static bool ContainsAll(IEnumerable<string> tokens, string[] terms)
        {
            var set = tokens as ISet<string> ?? tokens.ToHashSet();

            return terms.All(set.Contains);
        }

        private static decimal? ReadNumber(JsonObject arguments, string name) =>
            arguments[name] is JsonValue value && value.TryGetValue<decimal>(out var number) ? number : null;
    }

    public class ListCategoriesTool : IAgentTool
    {
        private readonly KnowledgeIndex _index;

        public ListCategoriesTool(KnowledgeIndex index) => _index = index;

        public string Name => "list_categories";

        public IReadOnlyCollection<ChatRole> Roles => ToolRoles.Everyone;

        public JsonObject Definition => ToolSchema.Function(
            Name,
            "List the product categories in the store with the number of products in each.",
            new JsonObject());

        public Task<string> ExecuteAsync(JsonObject arguments, AgentContext context, CancellationToken cancellationToken)
        {
            var categories = _index.Chunks("product")
                .Where(c => !string.IsNullOrWhiteSpace(c.Category))
                .GroupBy(c => c.Category!, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key)
                .Select(g => new { name = g.Key, products = g.Count() })
                .ToList();

            return Task.FromResult(categories.Count == 0
                ? ToolSchema.Error("no_categories")
                : JsonSerializer.Serialize(new { categories }));
        }
    }

    public class ListRecentOrdersTool : IAgentTool
    {
        private readonly OrderLookupClient _orders;

        public ListRecentOrdersTool(OrderLookupClient orders) => _orders = orders;

        public string Name => "list_recent_orders";

        public IReadOnlyCollection<ChatRole> Roles => ToolRoles.AdminOnly;

        public JsonObject Definition => ToolSchema.Function(
            Name,
            "Admin only. Summarise all store orders (count per status, revenue excluding cancelled) and list the most recent ones.",
            new JsonObject
            {
                ["status"] = ToolSchema.String("Optional status filter: Pending, Confirmed, Processing, Shipped, Delivered or Cancelled."),
                ["limit"] = ToolSchema.Number("How many orders to list, 1 to 20. Default 10.")
            });

        public async Task<string> ExecuteAsync(JsonObject arguments, AgentContext context, CancellationToken cancellationToken)
        {
            if (context.Role != ChatRole.Admin)
            {
                return ToolSchema.Error("not_allowed");
            }

            var all = await _orders.GetAsync(string.Empty, context, cancellationToken);

            if (all is null || all.Value.ValueKind != JsonValueKind.Array)
            {
                return ToolSchema.Error("orders_unavailable");
            }

            var orders = all.Value.EnumerateArray()
                .Select(OrderLookupClient.ToCard)
                .ToList();

            var status = OrderLookupClient.NormalizeStatus(arguments["status"]?.ToString());

            var matching = orders
                .Where(o => status is null || string.Equals(o.Status, status, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(o => o.OrderDate)
                .ToList();

            var listed = matching.Take(OrderLookupClient.ReadLimit(arguments, 10)).ToList();

            context.Orders.Clear();
            context.Orders.AddRange(listed);

            // No customer ids or names are passed to the model.
            return JsonSerializer.Serialize(new
            {
                totalOrders = orders.Count,
                matchingOrders = matching.Count,
                byStatus = orders.GroupBy(o => o.Status).ToDictionary(g => g.Key, g => g.Count()),
                revenueExcludingCancelledInr = orders.Where(o => o.Status != "Cancelled").Sum(o => o.TotalAmount),
                recent = listed.Select(o => new { orderNumber = o.OrderNumber, status = o.Status, totalInr = o.TotalAmount, date = o.OrderDate })
            });
        }
    }

    public class GetProductInventoryTool : IAgentTool
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly ServiceUrlOptions _services;

        public GetProductInventoryTool(IHttpClientFactory httpFactory, IOptions<ServiceUrlOptions> services)
        {
            _httpFactory = httpFactory;
            _services = services.Value;
        }

        public string Name => "get_product_inventory";

        public IReadOnlyCollection<ChatRole> Roles => ToolRoles.AdminOnly;

        public JsonObject Definition => ToolSchema.Function(
            Name,
            "Admin only. Get stock quantity, reorder level and low-stock flag for products whose name matches the query.",
            new JsonObject { ["product_name"] = ToolSchema.String("Product name or part of it, e.g. 'gaming laptop'.") },
            "product_name");

        public async Task<string> ExecuteAsync(JsonObject arguments, AgentContext context, CancellationToken cancellationToken)
        {
            if (context.Role != ChatRole.Admin)
            {
                return ToolSchema.Error("not_allowed");
            }

            var words = (arguments["product_name"]?.GetValue<string>() ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (words.Length == 0)
            {
                return ToolSchema.Error("product_name_required");
            }

            var rows = await InventoryFeed.GetAsync(_httpFactory, _services, context, cancellationToken);

            if (rows is null)
            {
                return ToolSchema.Error("inventory_unavailable");
            }

            var matches = rows
                .Where(i => words.All(w => i.name.Contains(w, StringComparison.OrdinalIgnoreCase)))
                .Take(5)
                .ToList();

            return matches.Count == 0
                ? ToolSchema.Error("no_matching_product")
                : JsonSerializer.Serialize(matches);
        }
    }

    internal record InventoryRow(string name, int quantity, int reorderLevel, bool isLowStock);

    internal static class InventoryFeed
    {
        // Read-only view of ProductService inventory; the database is never queried directly.
        public static async Task<List<InventoryRow>?> GetAsync(
            IHttpClientFactory httpFactory,
            ServiceUrlOptions services,
            AgentContext context,
            CancellationToken cancellationToken)
        {
            var http = httpFactory.CreateClient("catalog");

            using var request = new HttpRequestMessage(HttpMethod.Get, $"{services.ProductServiceUrl.TrimEnd('/')}/api/Inventory");

            if (!string.IsNullOrEmpty(context.BearerToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.BearerToken);
            }

            using var response = await http.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);

            return doc.RootElement.EnumerateArray()
                .Select(i => new InventoryRow(
                    i.GetProperty("productName").GetString() ?? string.Empty,
                    i.GetProperty("quantity").GetInt32(),
                    i.GetProperty("reorderLevel").GetInt32(),
                    i.GetProperty("isLowStock").GetBoolean()))
                .ToList();
        }
    }

    public class ListInventoryTool : IAgentTool
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly ServiceUrlOptions _services;

        public ListInventoryTool(IHttpClientFactory httpFactory, IOptions<ServiceUrlOptions> services)
        {
            _httpFactory = httpFactory;
            _services = services.Value;
        }

        public string Name => "list_inventory";

        public IReadOnlyCollection<ChatRole> Roles => ToolRoles.AdminOnly;

        public JsonObject Definition => ToolSchema.Function(
            Name,
            "Admin only. List products from live inventory filtered by reorder level, quantity or low-stock status. Use for questions like 'items with reorder level less than 10' or 'what is low on stock'.",
            new JsonObject
            {
                ["reorder_level_below"] = ToolSchema.Number("Only products whose reorder level is strictly less than this."),
                ["quantity_below"] = ToolSchema.Number("Only products whose stock quantity is strictly less than this."),
                ["low_stock_only"] = new JsonObject { ["type"] = "boolean", ["description"] = "Only products at or below their reorder level." },
                ["limit"] = ToolSchema.Number("Max products to return, 1 to 50. Default 20.")
            });

        public async Task<string> ExecuteAsync(JsonObject arguments, AgentContext context, CancellationToken cancellationToken)
        {
            if (context.Role != ChatRole.Admin)
            {
                return ToolSchema.Error("not_allowed");
            }

            var rows = await InventoryFeed.GetAsync(_httpFactory, _services, context, cancellationToken);

            if (rows is null)
            {
                return ToolSchema.Error("inventory_unavailable");
            }

            var reorderBelow = ReadInt(arguments, "reorder_level_below");
            var quantityBelow = ReadInt(arguments, "quantity_below");
            var lowOnly = arguments["low_stock_only"] is JsonValue b && b.TryGetValue<bool>(out var flag) && flag;
            var limit = Math.Clamp(ReadInt(arguments, "limit") ?? 20, 1, 50);

            var matches = rows
                .Where(i => reorderBelow is null || i.reorderLevel < reorderBelow)
                .Where(i => quantityBelow is null || i.quantity < quantityBelow)
                .Where(i => !lowOnly || i.isLowStock)
                .OrderBy(i => i.reorderLevel)
                .ThenBy(i => i.name)
                .ToList();

            return JsonSerializer.Serialize(new { totalMatches = matches.Count, items = matches.Take(limit) });
        }

        private static int? ReadInt(JsonObject arguments, string name) =>
            arguments[name] is JsonValue v && v.TryGetValue<decimal>(out var n) ? (int)n : null;
    }

    public class SearchAdminHelpTool : IAgentTool
    {
        private readonly KnowledgeIndex _index;

        public SearchAdminHelpTool(KnowledgeIndex index) => _index = index;

        public string Name => "search_admin_help";

        public IReadOnlyCollection<ChatRole> Roles => ToolRoles.AdminOnly;

        public JsonObject Definition => ToolSchema.Function(
            Name,
            "Admin only. Search the admin guide (orders, products, import, bulk update, categories, inventory, dashboard).",
            new JsonObject { ["query"] = ToolSchema.String("The admin's question in a few words.") },
            "query");

        public async Task<string> ExecuteAsync(JsonObject arguments, AgentContext context, CancellationToken cancellationToken)
        {
            if (context.Role != ChatRole.Admin)
            {
                return ToolSchema.Error("not_allowed");
            }

            var hits = await _index.SearchAsync(arguments["query"]?.GetValue<string>() ?? string.Empty, "admin-faq", 3, cancellationToken);

            foreach (var hit in hits)
            {
                if (!context.Citations.Any(c => c.Title == hit.Chunk.Title))
                {
                    context.Citations.Add(new Citation { Title = hit.Chunk.Title, Source = hit.Chunk.Source });
                }
            }

            return hits.Count == 0
                ? ToolSchema.Error("no_relevant_article_found")
                : JsonSerializer.Serialize(hits.Select(h => new { title = h.Chunk.Title, text = h.Chunk.Text }));
        }
    }
}
