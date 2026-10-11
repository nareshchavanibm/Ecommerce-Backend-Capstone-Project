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
            "List the signed-in customer's most recent orders. Use when they do not know the order number.",
            new JsonObject());

        public async Task<string> ExecuteAsync(JsonObject arguments, AgentContext context, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(context.BearerToken))
            {
                return ToolSchema.Error("login_required");
            }

            var orders = await _orders.GetAsync("my-orders", context, cancellationToken);

            if (orders is null || orders.Value.ValueKind != JsonValueKind.Array)
            {
                return JsonSerializer.Serialize(Array.Empty<OrderCard>());
            }

            var cards = orders.Value.EnumerateArray()
                .Select(OrderLookupClient.ToCard)
                .OrderByDescending(o => o.OrderDate)
                .Take(5)
                .ToList();

            return JsonSerializer.Serialize(cards);
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
                ["limit"] = ToolSchema.Number("How many recent orders to list, 1 to 20. Default 10.")
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
                .Select(o => new
                {
                    OrderNumber = o.GetProperty("orderNumber").GetString() ?? string.Empty,
                    Status = o.GetProperty("status").GetString() ?? string.Empty,
                    Total = o.GetProperty("totalAmount").GetDecimal(),
                    Date = o.GetProperty("orderDate").GetDateTime()
                })
                .ToList();

            var status = arguments["status"]?.GetValue<string>();
            var limit = arguments["limit"] is JsonValue v && v.TryGetValue<int>(out var l) ? Math.Clamp(l, 1, 20) : 10;

            var recent = orders
                .Where(o => string.IsNullOrWhiteSpace(status) || string.Equals(o.Status, status, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(o => o.Date)
                .Take(limit)
                .Select(o => new { orderNumber = o.OrderNumber, status = o.Status, totalInr = o.Total, date = o.Date });

            // No customer ids or names are passed to the model.
            return JsonSerializer.Serialize(new
            {
                totalOrders = orders.Count,
                byStatus = orders.GroupBy(o => o.Status).ToDictionary(g => g.Key, g => g.Count()),
                revenueExcludingCancelledInr = orders.Where(o => o.Status != "Cancelled").Sum(o => o.Total),
                recent
            });
        }
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
