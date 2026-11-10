using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ChatService.Api.Models;

namespace ChatService.Api.Agents
{
    // Answers from retrieval and tools alone, used when no language model is available.
    public partial class OfflineResponder
    {
        [GeneratedRegex(@"^\s*(hi|hello|hey|good (morning|afternoon|evening)|namaste)\b[\s!.,]*$", RegexOptions.IgnoreCase)]
        private static partial Regex Greeting();

        [GeneratedRegex(@"^\s*(thanks|thank you|thx|ok(ay)?|great|bye|goodbye)\b[\s!.,]*$", RegexOptions.IgnoreCase)]
        private static partial Regex Thanks();

        [GeneratedRegex(@"\b(human|person|agent|representative|someone)\b|talk to|speak to", RegexOptions.IgnoreCase)]
        private static partial Regex WantsHuman();

        [GeneratedRegex(@"\b(my (\w+ )?orders?|where is my|order status|track(ing)? (my )?order|orders|(recent|latest|past|previous|delivered|shipped|pending|confirmed|processing|cancell?ed) orders?)\b", RegexOptions.IgnoreCase)]
        private static partial Regex OrderIntent();

        [GeneratedRegex(@"\bcategories\b|\bcategory list\b", RegexOptions.IgnoreCase)]
        private static partial Regex CategoriesIntent();

        [GeneratedRegex(@"\b(list|all|show|display)\b|\bproducts?\b|\bcategory\b|\bwhat do you (sell|have)\b", RegexOptions.IgnoreCase)]
        private static partial Regex ListProductsIntent();

        [GeneratedRegex(@"\borders\b", RegexOptions.IgnoreCase)]
        private static partial Regex OrdersWord();

        [GeneratedRegex(@"\b(how|what does)\b", RegexOptions.IgnoreCase)]
        private static partial Regex HowTo();

        [GeneratedRegex(@"\b(pending|confirmed|processing|shipped|delivered|cancell?ed)\b", RegexOptions.IgnoreCase)]
        private static partial Regex StatusWord();

        [GeneratedRegex(@"ORD-[A-Z0-9]{6,12}")]
        private static partial Regex OrderNumber();

        private readonly AgentRunner _runner;

        public OfflineResponder(AgentRunner runner)
        {
            _runner = runner;
        }

        public async Task<AgentResult> RespondAsync(
            AgentDefinition agent,
            string message,
            AgentContext context,
            CancellationToken cancellationToken)
        {
            if (context.Role == ChatRole.Admin)
            {
                return await RespondAdminAsync(message, context, cancellationToken);
            }

            if (Greeting().IsMatch(message))
            {
                return Say("Hello! I can look up your orders, answer questions about the store, and suggest products. What do you need?");
            }

            if (Thanks().IsMatch(message))
            {
                return Say("You're welcome! Ask me anything else about your orders, the store or products.");
            }

            if (WantsHuman().IsMatch(message))
            {
                context.Escalate = true;
                return Say("Sure, you can reach a person through the support details below.");
            }

            var number = OrderNumber().Match(message.ToUpperInvariant());

            if (number.Success && _runner.Tool("lookup_order", context.Role) is { } lookup)
            {
                var payload = await lookup.ExecuteAsync(
                    new JsonObject { ["order_number"] = number.Value }, context, cancellationToken);

                if (context.Order is not null)
                {
                    return Say($"Order {context.Order.OrderNumber} is {context.Order.Status}.");
                }

                return Say(payload.Contains("login_required")
                    ? "Please sign in to look up an order."
                    : "I could not find that order on your account. Check the order number and that you are signed in with the account that placed it.");
            }

            if (OrderIntent().IsMatch(message) && _runner.Tool("list_my_orders", context.Role) is { } list)
            {
                var filter = new JsonObject();

                if (StatusWord().Match(message) is { Success: true } word)
                {
                    filter["status"] = word.Value;
                }

                var payload = await list.ExecuteAsync(filter, context, cancellationToken);

                if (payload.Contains("login_required"))
                {
                    return Say("Please sign in to see your orders.");
                }

                using var doc = JsonDocument.Parse(payload);
                var total = doc.RootElement.GetProperty("totalMatches").GetInt32();
                var label = OrderLabel(filter["status"]?.ToString());

                if (total == 0)
                {
                    return Say($"You do not have any {label}.");
                }

                return Say($"You have {total} {label}." +
                           (total > context.Orders.Count ? $" Showing the newest {context.Orders.Count}." : string.Empty));
            }

            if (CategoriesIntent().IsMatch(message) && await ListCategoriesAsync(context, cancellationToken) is { } categoryReply)
            {
                return categoryReply;
            }

            if (ListProductsIntent().IsMatch(message) && await ListProductsAsync(message, context, cancellationToken) is { } listReply)
            {
                return listReply;
            }

            if (agent.Name == AgentCatalog.Recommendation && _runner.Tool("search_products", context.Role) is { } products)
            {
                var found = await FindProductsAsync(products, message, context, cancellationToken);

                if (found)
                {
                    return Say("Here are some products that may suit you.");
                }
            }

            if (_runner.Tool("search_knowledge", context.Role) is { } knowledge)
            {
                var payload = await knowledge.ExecuteAsync(
                    new JsonObject { ["query"] = message }, context, cancellationToken);

                if (!payload.Contains("no_relevant_article_found"))
                {
                    using var doc = JsonDocument.Parse(payload);
                    var text = doc.RootElement[0].GetProperty("text").GetString() ?? string.Empty;

                    return Say(Clean(text));
                }
            }

            // A question the help articles do not cover may still be a product search.
            if (agent.Name != AgentCatalog.Recommendation &&
                _runner.Tool("search_products", context.Role) is { } fallback &&
                await FindProductsAsync(fallback, message, context, cancellationToken))
            {
                return Say("I found these products in the store.");
            }

            context.Escalate = true;

            return Say("I do not have an answer for that. You can reach a person through the support details below.");
        }

        [GeneratedRegex(@"\b(recent|latest|summary|summari[sz]e|overview|how many|pending orders|(show|list) (all )?orders)\b", RegexOptions.IgnoreCase)]
        private static partial Regex RecentOrdersIntent();

        [GeneratedRegex(@"\b(stock|quantity|re-?order level|inventory)( level)?\s+(of|for)\s+(the\s+)?(?<name>.+?)[\s?.!]*$", RegexOptions.IgnoreCase)]
        private static partial Regex InventoryIntent();

        [GeneratedRegex(@"re-?order level\s+(less than|below|under|<)\s*(?<n>\d+)", RegexOptions.IgnoreCase)]
        private static partial Regex ReorderBelowIntent();

        [GeneratedRegex(@"\blow (on )?stock\b", RegexOptions.IgnoreCase)]
        private static partial Regex LowStockIntent();

        private async Task<AgentResult> RespondAdminAsync(
            string message,
            AgentContext context,
            CancellationToken cancellationToken)
        {
            if (Greeting().IsMatch(message))
            {
                return Say("Hello! I can look up an order, show recent orders, or explain how to manage products, categories and inventory.");
            }

            if (Thanks().IsMatch(message))
            {
                return Say("You're welcome! Ask me anything else about managing the store.");
            }

            var number = OrderNumber().Match(message.ToUpperInvariant());

            if (number.Success && _runner.Tool("lookup_order", context.Role) is { } lookup)
            {
                await lookup.ExecuteAsync(new JsonObject { ["order_number"] = number.Value }, context, cancellationToken);

                return Say(context.Order is null
                    ? "I could not find that order."
                    : $"Order {context.Order.OrderNumber} is {context.Order.Status}.");
            }

            var below = ReorderBelowIntent().Match(message);

            if ((below.Success || LowStockIntent().IsMatch(message)) &&
                _runner.Tool("list_inventory", context.Role) is { } listing)
            {
                var filter = new JsonObject();

                if (below.Success)
                {
                    filter["reorder_level_below"] = int.Parse(below.Groups["n"].Value);
                }
                else
                {
                    filter["low_stock_only"] = true;
                }

                var payload = await listing.ExecuteAsync(filter, context, cancellationToken);

                if (payload.Contains("inventory_unavailable"))
                {
                    return Say("I could not load inventory right now.");
                }

                using var doc = JsonDocument.Parse(payload);
                var total = doc.RootElement.GetProperty("totalMatches").GetInt32();
                var lines = doc.RootElement.GetProperty("items").EnumerateArray().Select(i =>
                    $"{i.GetProperty("name").GetString()}: stock {i.GetProperty("quantity").GetInt32()}, reorder level {i.GetProperty("reorderLevel").GetInt32()}").ToList();

                return Say(total == 0
                    ? "No products match."
                    : $"{total} products match:\n{string.Join("\n", lines)}" + (total > lines.Count ? $"\n...and {total - lines.Count} more." : string.Empty));
            }

            if (InventoryIntent().Match(message) is { Success: true } stock &&
                _runner.Tool("get_product_inventory", context.Role) is { } inventory)
            {
                var payload = await inventory.ExecuteAsync(
                    new JsonObject { ["product_name"] = stock.Groups["name"].Value }, context, cancellationToken);

                if (payload.Contains("no_matching_product") || payload.Contains("inventory_unavailable"))
                {
                    return Say("I could not find inventory for that product.");
                }

                using var doc = JsonDocument.Parse(payload);
                var lines = doc.RootElement.EnumerateArray().Select(i =>
                    $"{i.GetProperty("name").GetString()}: stock {i.GetProperty("quantity").GetInt32()}, reorder level {i.GetProperty("reorderLevel").GetInt32()}" +
                    (i.GetProperty("isLowStock").GetBoolean() ? " (low stock)" : string.Empty));

                return Say(string.Join("; ", lines) + ".");
            }

            if (OrdersWord().IsMatch(message) && !HowTo().IsMatch(message) &&
                StatusWord().Match(message) is { Success: true } wanted &&
                _runner.Tool("list_recent_orders", context.Role) is { } byStatus)
            {
                var payload = await byStatus.ExecuteAsync(
                    new JsonObject { ["status"] = wanted.Value, ["limit"] = OrderLookupClient.MaxListed }, context, cancellationToken);

                if (payload.Contains("orders_unavailable"))
                {
                    return Say("I could not load orders right now.");
                }

                using var doc = JsonDocument.Parse(payload);
                var matching = doc.RootElement.GetProperty("matchingOrders").GetInt32();
                var label = OrderLabel(wanted.Value);

                return Say(matching == 0
                    ? $"There are no {label}."
                    : $"There are {matching} {label}." + (matching > context.Orders.Count ? $" Showing the newest {context.Orders.Count}." : string.Empty));
            }

            if (!HowTo().IsMatch(message))
            {
                if (CategoriesIntent().IsMatch(message) && await ListCategoriesAsync(context, cancellationToken) is { } categoryReply)
                {
                    return categoryReply;
                }

                if (ProductWord().IsMatch(message) && await ListProductsAsync(message, context, cancellationToken) is { } listReply)
                {
                    return listReply;
                }
            }

            // Guide questions ("how do I ...") take priority over the order summary.
            if (_runner.Tool("search_admin_help", context.Role) is { } help)
            {
                var payload = await help.ExecuteAsync(new JsonObject { ["query"] = message }, context, cancellationToken);

                if (!payload.Contains("no_relevant_article_found") && !RecentOrdersIntent().IsMatch(message))
                {
                    using var doc = JsonDocument.Parse(payload);
                    return Say(Clean(doc.RootElement[0].GetProperty("text").GetString() ?? string.Empty));
                }
            }

            if (RecentOrdersIntent().IsMatch(message) && _runner.Tool("list_recent_orders", context.Role) is { } recent)
            {
                var payload = await recent.ExecuteAsync(new JsonObject(), context, cancellationToken);

                if (payload.Contains("orders_unavailable"))
                {
                    return Say("I could not load orders right now.");
                }

                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;

                var statuses = string.Join(", ", root.GetProperty("byStatus").EnumerateObject().Select(p => $"{p.Value.GetInt32()} {p.Name}"));
                var latest = root.GetProperty("recent").EnumerateArray().Take(5)
                    .Select(o => $"{o.GetProperty("orderNumber").GetString()} ({o.GetProperty("status").GetString()})");

                return Say($"There are {root.GetProperty("totalOrders").GetInt32()} orders" +
                           (statuses.Length > 0 ? $": {statuses}." : ".") +
                           $" Latest: {string.Join(", ", latest)}.");
            }

            return Say("I do not have an answer for that. Try asking about an order number, recent orders, or how to manage products, categories and inventory.");
        }

        private static async Task<bool> FindProductsAsync(
            IAgentTool tool,
            string message,
            AgentContext context,
            CancellationToken cancellationToken)
        {
            await tool.ExecuteAsync(new JsonObject { ["query"] = message }, context, cancellationToken);
            return context.Products.Count > 0;
        }

        // Drops the section prefix and markdown emphasis added when articles were chunked.
        private static string Clean(string text)
        {
            var colon = text.IndexOf(": ", StringComparison.Ordinal);

            if (colon is > 0 and < 40)
            {
                text = text[(colon + 2)..];
            }

            return text.Replace("**", string.Empty).Trim();
        }

        [GeneratedRegex(@"\bproducts?\b", RegexOptions.IgnoreCase)]
        private static partial Regex ProductWord();

        private async Task<AgentResult?> ListCategoriesAsync(AgentContext context, CancellationToken cancellationToken)
        {
            if (_runner.Tool("list_categories", context.Role) is not { } tool)
            {
                return null;
            }

            var payload = await tool.ExecuteAsync(new JsonObject(), context, cancellationToken);

            if (payload.Contains("no_categories"))
            {
                return null;
            }

            using var doc = JsonDocument.Parse(payload);
            var names = doc.RootElement.GetProperty("categories").EnumerateArray()
                .Select(c => $"{c.GetProperty("name").GetString()} ({c.GetProperty("products").GetInt32()})");

            return Say($"Our categories: {string.Join(", ", names)}.");
        }

        private async Task<AgentResult?> ListProductsAsync(string message, AgentContext context, CancellationToken cancellationToken)
        {
            if (_runner.Tool("list_products", context.Role) is not { } tool)
            {
                return null;
            }

            var payload = await tool.ExecuteAsync(new JsonObject { ["query"] = message }, context, cancellationToken);

            if (context.Products.Count == 0)
            {
                return null;
            }

            using var doc = JsonDocument.Parse(payload);
            var total = doc.RootElement.GetProperty("totalMatches").GetInt32();

            return Say($"I found {total} matching {(total == 1 ? "product" : "products")}." +
                       (total > context.Products.Count ? $" Showing the first {context.Products.Count}, cheapest first." : string.Empty));
        }

        private static AgentResult Say(string reply) => new(reply, false, 0, 0);

        private static string OrderLabel(string? status) =>
            OrderLookupClient.NormalizeStatus(status) is { } s ? $"{s.ToLowerInvariant()} orders" : "orders";
    }
}
