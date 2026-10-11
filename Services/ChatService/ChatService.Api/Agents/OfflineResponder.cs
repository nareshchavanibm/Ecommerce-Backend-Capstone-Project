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

        [GeneratedRegex(@"\b(my orders?|where is my|order status|track(ing)? (my )?order|recent orders?)\b", RegexOptions.IgnoreCase)]
        private static partial Regex OrderIntent();

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
                var payload = await list.ExecuteAsync(new JsonObject(), context, cancellationToken);

                if (payload.Contains("login_required"))
                {
                    return Say("Please sign in to see your orders.");
                }

                var orders = JsonSerializer.Deserialize<List<Models.OrderCard>>(payload) ?? new();

                if (orders.Count == 0)
                {
                    return Say("You do not have any orders yet.");
                }

                context.Order = orders[0];

                return Say($"Your latest order {context.Order.OrderNumber} is {context.Order.Status}. You can see all orders under Orders in the menu.");
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

        private static AgentResult Say(string reply) => new(reply, false, 0, 0);
    }
}
