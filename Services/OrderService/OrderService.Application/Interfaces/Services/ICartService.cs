using OrderService.Application.DTOs.Cart;

namespace OrderService.Application.Interfaces.Services
{
    public interface ICartService
    {
        Task<CartResponseDto> GetCartAsync(int userId);

        Task<CartResponseDto> AddItemAsync(int userId, AddCartItemRequest request);

        Task<CartResponseDto> UpdateItemAsync(int userId, int productId, int quantity);

        Task<CartResponseDto> RemoveItemAsync(int userId, int productId);

        Task ClearAsync(int userId);
    }
}
