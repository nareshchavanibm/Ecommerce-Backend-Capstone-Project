using ProductService.Application.DTOs.Promotions;

namespace ProductService.Application.Interfaces.Services
{
    public interface IPromotionService
    {
        Task<List<PromotionResponseDto>> GetAllAsync();

        // Active and inside their start/end window; what shoppers see.
        Task<List<PromotionResponseDto>> GetActiveAsync();

        Task<PromotionResponseDto?> GetByIdAsync(int id);

        Task<PromotionResponseDto> CreateAsync(PromotionCreateDto request);

        Task<PromotionResponseDto?> UpdateAsync(int id, PromotionCreateDto request);

        Task<bool> DeleteAsync(int id);
    }
}
