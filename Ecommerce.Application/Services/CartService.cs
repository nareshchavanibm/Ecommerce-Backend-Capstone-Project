using ECommerce.Application.DTOs.Cart;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Interfaces.Repositories;
using ECommerce.Domain.Entities;

namespace ECommerce.Application.Services;

public class CartService : ICartService
{
    private readonly ICartRepository _cartRepository;
    private readonly IProductRepository _productRepository;

    public CartService(
        ICartRepository cartRepository,
        IProductRepository productRepository)
    {
        _cartRepository = cartRepository;
        _productRepository = productRepository;
    }

    public async Task<CartDto> GetCartAsync(int userId)
    {
        var cart = await _cartRepository.GetByUserIdAsync(userId);

        if (cart == null)
        {
            cart = new Cart
            {
                UserId = userId
            };

            await _cartRepository.AddAsync(cart);
            await _cartRepository.SaveChangesAsync();

            cart = await _cartRepository.GetByUserIdAsync(userId);

            if (cart == null)
                throw new InvalidOperationException(
                    "Cart could not be created.");
        }

        return MapToDto(cart);
    }

    public async Task<CartDto> AddItemAsync(
        int userId,
        AddCartItemDto dto)
    {
        if (dto.Quantity <= 0)
            throw new ArgumentException(
                "Quantity must be greater than zero.");

        var product = await _productRepository
            .GetByIdAsync(dto.ProductId);

        if (product == null)
            throw new ArgumentException(
                "Product was not found.");

        var cart = await _cartRepository
            .GetByUserIdAsync(userId);

        if (cart == null)
        {
            cart = new Cart
            {
                UserId = userId
            };

            await _cartRepository.AddAsync(cart);
            await _cartRepository.SaveChangesAsync();

            cart = await _cartRepository.GetByUserIdAsync(userId);

            if (cart == null)
                throw new InvalidOperationException(
                    "Cart could not be created.");
        }

        var existingItem = cart.CartItems
            .FirstOrDefault(x => x.ProductId == dto.ProductId);

        if (existingItem != null)
        {
            existingItem.Quantity += dto.Quantity;
        }
        else
        {
            cart.CartItems.Add(new CartItem
            {
                ProductId = product.Id,
                Quantity = dto.Quantity
            });
        }

        await _cartRepository.UpdateAsync(cart);
        await _cartRepository.SaveChangesAsync();

        var updatedCart = await _cartRepository
            .GetByUserIdAsync(userId);

        if (updatedCart == null)
            throw new InvalidOperationException(
                "Cart could not be retrieved.");

        return MapToDto(updatedCart);
    }

    public async Task<CartDto> UpdateItemAsync(
        int userId,
        int productId,
        UpdateCartItemDto dto)
    {
        if (dto.Quantity <= 0)
            throw new ArgumentException(
                "Quantity must be greater than zero.");

        var cart = await _cartRepository
            .GetByUserIdAsync(userId);

        if (cart == null)
            throw new ArgumentException(
                "Cart was not found.");

        var item = cart.CartItems
            .FirstOrDefault(x => x.ProductId == productId);

        if (item == null)
            throw new ArgumentException(
                "Product is not in the cart.");

        item.Quantity = dto.Quantity;

        await _cartRepository.UpdateAsync(cart);
        await _cartRepository.SaveChangesAsync();

        var updatedCart = await _cartRepository
            .GetByUserIdAsync(userId);

        if (updatedCart == null)
            throw new InvalidOperationException(
                "Cart could not be retrieved.");

        return MapToDto(updatedCart);
    }

    public async Task<CartDto> RemoveItemAsync(
        int userId,
        int productId)
    {
        var cart = await _cartRepository
            .GetByUserIdAsync(userId);

        if (cart == null)
            throw new ArgumentException(
                "Cart was not found.");

        var item = cart.CartItems
            .FirstOrDefault(x => x.ProductId == productId);

        if (item == null)
            throw new ArgumentException(
                "Product is not in the cart.");

        cart.CartItems.Remove(item);

        await _cartRepository.UpdateAsync(cart);
        await _cartRepository.SaveChangesAsync();

        var updatedCart = await _cartRepository
            .GetByUserIdAsync(userId);

        if (updatedCart == null)
            throw new InvalidOperationException(
                "Cart could not be retrieved.");

        return MapToDto(updatedCart);
    }

    public async Task ClearCartAsync(int userId)
    {
        var cart = await _cartRepository
            .GetByUserIdAsync(userId);

        if (cart == null)
            return;

        cart.CartItems.Clear();

        await _cartRepository.UpdateAsync(cart);
        await _cartRepository.SaveChangesAsync();
    }

    private static CartDto MapToDto(Cart cart)
    {
        var items = cart.CartItems
            .Select(item => new CartItemDto
            {
                ProductId = item.ProductId,
                ProductName = item.Product.Name,
                Price = item.Product.Price,
                Quantity = item.Quantity,
                TotalPrice =
                    item.Product.Price * item.Quantity
            })
            .ToList();

        return new CartDto
        {
            Id = cart.Id,
            UserId = cart.UserId,
            Items = items,
            TotalAmount = items.Sum(x => x.TotalPrice)
        };
    }
}