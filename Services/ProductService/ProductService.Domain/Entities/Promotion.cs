using ProductService.Domain.Common;

namespace ProductService.Domain.Entities
{
    public enum PromotionType
    {
        Banner = 0,
        Ad = 1,
        Offer = 2
    }

    // Storefront content managed by admins: hero banners, ads and offers.
    public class Promotion : BaseEntity
    {
        public PromotionType Type { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Subtitle { get; set; } = string.Empty;

        public string ImageUrl { get; set; } = string.Empty;

        // Relative app path (e.g. /products?categoryId=2) or an absolute http(s) URL.
        public string LinkUrl { get; set; } = string.Empty;

        // Short highlight such as "20% OFF" (offers).
        public string DiscountText { get; set; } = string.Empty;

        public string PromoCode { get; set; } = string.Empty;

        public DateTime? StartsAt { get; set; }

        public DateTime? EndsAt { get; set; }

        public bool IsActive { get; set; } = true;

        public int DisplayOrder { get; set; }
    }
}
