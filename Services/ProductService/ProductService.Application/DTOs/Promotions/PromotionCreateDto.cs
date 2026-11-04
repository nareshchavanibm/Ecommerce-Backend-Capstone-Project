namespace ProductService.Application.DTOs.Promotions
{
    public class PromotionCreateDto
    {
        // Banner, Ad or Offer.
        public string Type { get; set; } = "Banner";

        public string Title { get; set; } = string.Empty;

        public string Subtitle { get; set; } = string.Empty;

        public string ImageUrl { get; set; } = string.Empty;

        public string LinkUrl { get; set; } = string.Empty;

        public string DiscountText { get; set; } = string.Empty;

        public string PromoCode { get; set; } = string.Empty;

        public DateTime? StartsAt { get; set; }

        public DateTime? EndsAt { get; set; }

        public bool IsActive { get; set; } = true;

        public int DisplayOrder { get; set; }
    }
}
