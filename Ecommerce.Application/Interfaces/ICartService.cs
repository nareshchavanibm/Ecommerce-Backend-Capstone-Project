using ECommerce.Application.DTOs.Cart;

namespace ECommerce.Application.Interfaces;

public interface ICartService
{
    Task<CartDto> GetCartAsync(int userId);

    Task<CartDto> AddItemAsync(
        int userId,
        AddCartItemDto dto);

    Task<CartDto> UpdateItemAsync(
        int userId,
        int productId,
        UpdateCartItemDto dto);

    Task<CartDto> RemoveItemAsync(
        int userId,
        int productId);

    Task ClearCartAsync(int userId);
}