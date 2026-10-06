using OrderService.Application.DTOs.Cart;
using OrderService.Application.Interfaces.Repositories;
using OrderService.Application.Interfaces.Services;
using OrderService.Domain.Entities;

namespace OrderService.Application.Services
{
    public class CartService : ICartService
    {
        private readonly ICartRepository _cartRepository;
        private readonly ICartItemRepository _cartItemRepository;

        public CartService(
            ICartRepository cartRepository,
            ICartItemRepository cartItemRepository)
        {
            _cartRepository = cartRepository;
            _cartItemRepository = cartItemRepository;
        }

        public async Task<CartResponseDto> GetCartAsync(int userId)
        {
            var cart = await _cartRepository.GetByUserIdAsync(userId);

            if (cart == null)
            {
                cart = new Cart
                {
                    UserId = userId
                };

                cart = await _cartRepository.AddAsync(cart);
            }

            var items = await _cartItemRepository.GetByCartIdAsync(cart.Id);

            return new CartResponseDto
            {
                Id = cart.Id,
                UserId = cart.UserId,
                CreatedAt = cart.CreatedAt,
                Items = items.Select(item => new CartItemDto
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice
                }).ToList()
            };
        }

        public async Task<CartResponseDto> AddItemAsync(
            int userId,
            CartItemDto dto)
        {
            var cart = await _cartRepository.GetByUserIdAsync(userId);

            if (cart == null)
            {
                cart = new Cart
                {
                    UserId = userId
                };

                cart = await _cartRepository.AddAsync(cart);
            }

            var existingItems =
                await _cartItemRepository.GetByCartIdAsync(cart.Id);

            var existingItem =
                existingItems.FirstOrDefault(x => x.ProductId == dto.ProductId);

            if (existingItem != null)
            {
                existingItem.Quantity += dto.Quantity;
                existingItem.UnitPrice = dto.UnitPrice;

                await _cartItemRepository.UpdateAsync(existingItem);
            }
            else
            {
                var cartItem = new CartItem
                {
                    CartId = cart.Id,
                    ProductId = dto.ProductId,
                    Quantity = dto.Quantity,
                    UnitPrice = dto.UnitPrice
                };

                await _cartItemRepository.AddAsync(cartItem);
            }

            return await GetCartAsync(userId);
        }

        public async Task<CartResponseDto> UpdateItemAsync(
            int userId,
            int productId,
            int quantity)
        {
            if (quantity <= 0)
            {
                throw new ArgumentException(
                    "Quantity must be greater than zero.");
            }

            var cart = await _cartRepository.GetByUserIdAsync(userId);

            if (cart == null)
            {
                throw new KeyNotFoundException("Cart not found.");
            }

            var items = await _cartItemRepository.GetByCartIdAsync(cart.Id);

            var item =
                items.FirstOrDefault(x => x.ProductId == productId);

            if (item == null)
            {
                throw new KeyNotFoundException("Cart item not found.");
            }

            item.Quantity = quantity;

            await _cartItemRepository.UpdateAsync(item);

            return await GetCartAsync(userId);
        }

        public async Task RemoveItemAsync(
            int userId,
            int productId)
        {
            var cart = await _cartRepository.GetByUserIdAsync(userId);

            if (cart == null)
            {
                throw new KeyNotFoundException("Cart not found.");
            }

            var items = await _cartItemRepository.GetByCartIdAsync(cart.Id);

            var item =
                items.FirstOrDefault(x => x.ProductId == productId);

            if (item == null)
            {
                throw new KeyNotFoundException("Cart item not found.");
            }

            await _cartItemRepository.DeleteAsync(item);
        }
    }
}