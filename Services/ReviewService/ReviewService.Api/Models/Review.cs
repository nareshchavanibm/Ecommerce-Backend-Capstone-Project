namespace ReviewService.Api.Models
{
    public class Review
    {
        public int Id { get; set; }

        public int ProductId { get; set; }

        public int UserId { get; set; }

        // First name and last initial only.
        public string ReviewerName { get; set; } = string.Empty;

        public int Rating { get; set; }

        public string? Title { get; set; }

        public string? Body { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
