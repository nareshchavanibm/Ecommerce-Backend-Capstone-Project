namespace ChatService.Api.Models
{
    public enum ChatRole
    {
        Anonymous,
        Customer,
        Admin
    }

    public class ChatConfig
    {
        public string Role { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string Intro { get; init; } = string.Empty;

        public string[] Suggestions { get; init; } = Array.Empty<string>();
    }

    // What each audience sees in the widget; the agents and tools enforce the same split on the server.
    public static class ChatProfiles
    {
        public static ChatConfig For(ChatRole role) => role == ChatRole.Admin
            ? new ChatConfig
            {
                Role = "admin",
                Title = "Admin assistant",
                Intro = "Ask about orders, recent order activity, or how to manage products, categories and inventory. This assistant is read-only.",
                Suggestions = new[]
                {
                    "Show recent orders",
                    "Look up an order by number",
                    "How do I import products?",
                    "How do I change an order status?"
                }
            }
            : new ChatConfig
            {
                Role = role == ChatRole.Customer ? "customer" : "anonymous",
                Title = "Shopping assistant",
                Intro = "Ask about an order, how the store works, or what to buy. Never share card numbers or passwords here.",
                Suggestions = new[]
                {
                    "Where is my order?",
                    "How do I cancel an order?",
                    "Recommend a gift under 1000"
                }
            };
    }
}
