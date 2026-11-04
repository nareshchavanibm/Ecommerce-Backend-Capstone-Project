using ProductService.Application.DTOs.Promotions;
using ProductService.Application.Interfaces.Repositories;
using ProductService.Application.Interfaces.Services;
using ProductService.Domain.Entities;

namespace ProductService.Application.Services
{
    public class PromotionService : IPromotionService
    {
        private readonly IPromotionRepository _promotionRepository;

        public PromotionService(IPromotionRepository promotionRepository)
        {
            _promotionRepository = promotionRepository;
        }

        public async Task<List<PromotionResponseDto>> GetAllAsync()
        {
            var promotions = await _promotionRepository.GetAllAsync();

            return promotions.Select(MapToResponse).ToList();
        }

        public async Task<List<PromotionResponseDto>> GetActiveAsync()
        {
            var promotions = await _promotionRepository.GetActiveAsync(DateTime.UtcNow);

            return promotions.Select(MapToResponse).ToList();
        }

        public async Task<PromotionResponseDto?> GetByIdAsync(int id)
        {
            var promotion = await _promotionRepository.GetByIdAsync(id);

            return promotion == null ? null : MapToResponse(promotion);
        }

        public async Task<PromotionResponseDto> CreateAsync(PromotionCreateDto request)
        {
            var promotion = new Promotion();

            Apply(promotion, request);

            await _promotionRepository.AddAsync(promotion);

            return MapToResponse(promotion);
        }

        public async Task<PromotionResponseDto?> UpdateAsync(int id, PromotionCreateDto request)
        {
            var promotion = await _promotionRepository.GetByIdAsync(id);

            if (promotion == null)
                return null;

            Apply(promotion, request);

            await _promotionRepository.UpdateAsync(promotion);

            return MapToResponse(promotion);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var promotion = await _promotionRepository.GetByIdAsync(id);

            if (promotion == null)
                return false;

            await _promotionRepository.DeleteAsync(promotion);

            return true;
        }

        private static void Apply(Promotion promotion, PromotionCreateDto request)
        {
            promotion.Type = Enum.Parse<PromotionType>(request.Type, true);
            promotion.Title = request.Title.Trim();
            promotion.Subtitle = request.Subtitle?.Trim() ?? string.Empty;
            promotion.ImageUrl = request.ImageUrl?.Trim() ?? string.Empty;
            promotion.LinkUrl = request.LinkUrl?.Trim() ?? string.Empty;
            promotion.DiscountText = request.DiscountText?.Trim() ?? string.Empty;
            promotion.PromoCode = request.PromoCode?.Trim() ?? string.Empty;
            promotion.StartsAt = ToUtc(request.StartsAt);
            promotion.EndsAt = ToUtc(request.EndsAt);
            promotion.IsActive = request.IsActive;
            promotion.DisplayOrder = request.DisplayOrder;
        }

        // Unspecified times (no zone in the payload) are treated as UTC.
        private static DateTime? ToUtc(DateTime? value) => value.HasValue
            ? value.Value.Kind == DateTimeKind.Local
                ? value.Value.ToUniversalTime()
                : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            : null;

        // EF returns datetime2 values with an unspecified kind; mark them UTC so JSON carries a "Z".
        private static DateTime? AsUtc(DateTime? value) =>
            value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;

        private static PromotionResponseDto MapToResponse(Promotion promotion) => new()
        {
            Id = promotion.Id,
            Type = promotion.Type.ToString(),
            Title = promotion.Title,
            Subtitle = promotion.Subtitle,
            ImageUrl = promotion.ImageUrl,
            LinkUrl = promotion.LinkUrl,
            DiscountText = promotion.DiscountText,
            PromoCode = promotion.PromoCode,
            StartsAt = AsUtc(promotion.StartsAt),
            EndsAt = AsUtc(promotion.EndsAt),
            IsActive = promotion.IsActive,
            DisplayOrder = promotion.DisplayOrder,
            CreatedAt = DateTime.SpecifyKind(promotion.CreatedAt, DateTimeKind.Utc)
        };
    }
}
