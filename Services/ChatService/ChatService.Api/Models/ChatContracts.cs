namespace ChatService.Api.Models
{
    public class ChatTurn
    {
        public string Role { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;
    }

    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;

        public string? ConversationId { get; set; }

        // Last turns kept by the client; the server clamps and validates them.
        public List<ChatTurn>? History { get; set; }

        // Agent that answered the previous turn, used to keep follow-ups on the same agent.
        public string? Agent { get; set; }
    }

    public class Citation
    {
        public string Title { get; set; } = string.Empty;

        public string Source { get; set; } = string.Empty;
    }

    public class ProductCard
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public decimal Price { get; set; }

        public string? ImageUrl { get; set; }

        public string Category { get; set; } = string.Empty;
    }

    public class OrderCard
    {
        public string OrderNumber { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public decimal TotalAmount { get; set; }

        public DateTime OrderDate { get; set; }

        public List<string> Items { get; set; } = new();
    }

    public class ChatResponse
    {
        public string ConversationId { get; set; } = string.Empty;

        public string Reply { get; set; } = string.Empty;

        public string Agent { get; set; } = string.Empty;

        public List<Citation> Citations { get; set; } = new();

        public List<ProductCard> Products { get; set; } = new();

        public OrderCard? Order { get; set; }

        // True when the customer should be pointed to a human.
        public bool Escalate { get; set; }

        public string? SupportEmail { get; set; }
    }

    public class FeedbackRequest
    {
        public string ConversationId { get; set; } = string.Empty;

        // "up" or "down"
        public string Rating { get; set; } = string.Empty;

        public string? Agent { get; set; }
    }
}
