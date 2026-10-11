using Microsoft.Extensions.Logging;
using OrderService.Application.DTOs.Cart;
using OrderService.Application.Exceptions;
using OrderService.Application.Interfaces.Clients;
using OrderService.Application.Interfaces.Repositories;
using OrderService.Application.Interfaces.Services;
using OrderService.Domain.Entities;

namespace OrderService.Application.Services
{
    public class CartService(
        ICartRepository cartRepository,
        ICartItemRepository cartItemRepository,
        IProductCatalog productCatalog,
        ILogger<CartService> logger) : ICartService
    {
        // Sanity limit per line, not a stock rule; stock is checked when the order is placed.
        private const int MaxQuantityPerItem = 1000;

        public async Task<CartResponseDto> GetCartAsync(int userId)
        {
            var cart = await GetOrCreateCartAsync(userId);

            return await BuildResponseAsync(cart);
        }

        public async Task<CartResponseDto> AddItemAsync(
            int userId,
            AddCartItemRequest request)
        {
            ValidateQuantity(request.Quantity);

            if (request.ProductId <= 0)
            {
                throw new BusinessRuleException("Product ID must be greater than 0.");
            }

            var products = await productCatalog.GetProductsAsync([request.ProductId]);

            if (!products.TryGetValue(request.ProductId, out var product))
            {
                throw new NotFoundException("Product not found.");
            }

            if (!product.IsActive)
            {
                throw new BusinessRuleException("This product is no longer available.");
            }

            var cart = await GetOrCreateCartAsync(userId);

            var existing = (await cartItemRepository.GetByCartIdAsync(cart.Id))
                .FirstOrDefault(x => x.ProductId == request.ProductId);

            if (existing != null)
            {
                ValidateQuantity(existing.Quantity + request.Quantity);

                existing.Quantity += request.Quantity;
                existing.UnitPrice = product.Price;

                await cartItemRepository.UpdateAsync(existing);
            }
            else
            {
                await cartItemRepository.AddAsync(new CartItem
                {
                    CartId = cart.Id,
                    ProductId = request.ProductId,
                    Quantity = request.Quantity,
                    UnitPrice = product.Price
                });
            }

            return await BuildResponseAsync(cart);
        }

        public async Task<CartResponseDto> UpdateItemAsync(
            int userId,
            int productId,
            int quantity)
        {
            ValidateQuantity(quantity);

            var cart = await GetOrCreateCartAsync(userId);

            var item = (await cartItemRepository.GetByCartIdAsync(cart.Id))
                .FirstOrDefault(x => x.ProductId == productId)
                ?? throw new NotFoundException("Cart item not found.");

            item.Quantity = quantity;

            await cartItemRepository.UpdateAsync(item);

            return await BuildResponseAsync(cart);
        }

        public async Task<CartResponseDto> RemoveItemAsync(
            int userId,
            int productId)
        {
            var cart = await GetOrCreateCartAsync(userId);

            var item = (await cartItemRepository.GetByCartIdAsync(cart.Id))
                .FirstOrDefault(x => x.ProductId == productId)
                ?? throw new NotFoundException("Cart item not found.");

            await cartItemRepository.DeleteAsync(item);

            return await BuildResponseAsync(cart);
        }

        public async Task ClearAsync(int userId)
        {
            var cart = await cartRepository.GetByUserIdAsync(userId);

            if (cart == null)
            {
                return;
            }

            var items = (await cartItemRepository.GetByCartIdAsync(cart.Id)).ToList();

            if (items.Count > 0)
            {
                await cartItemRepository.DeleteRangeAsync(items);
            }
        }

        private async Task<Cart> GetOrCreateCartAsync(int userId)
        {
            return await cartRepository.GetByUserIdAsync(userId)
                ?? await cartRepository.AddAsync(new Cart { UserId = userId });
        }

        private async Task<CartResponseDto> BuildResponseAsync(Cart cart)
        {
            var items = (await cartItemRepository.GetByCartIdAsync(cart.Id)).ToList();

            IReadOnlyDictionary<int, CatalogProduct> products =
                new Dictionary<int, CatalogProduct>();

            if (items.Count > 0)
            {
                try
                {
                    products = await productCatalog.GetProductsAsync(
                        items.Select(x => x.ProductId));
                }
                catch (DependencyUnavailableException ex)
                {
                    // Show the saved prices rather than failing the whole cart.
                    logger.LogWarning(ex, "Product catalog unavailable while building cart {CartId}.", cart.Id);
                }
            }

            var lines = items.Select(item =>
            {
                var found = products.TryGetValue(item.ProductId, out var product);
                var price = found ? product!.Price : item.UnitPrice;

                return new CartItemDto
                {
                    ProductId = item.ProductId,
                    ProductName = found ? product!.Name : $"Product #{item.ProductId}",
                    Price = price,
                    Quantity = item.Quantity,
                    TotalPrice = price * item.Quantity
                };
            }).ToList();

            return new CartResponseDto
            {
                Id = cart.Id,
                UserId = cart.UserId,
                CreatedAt = cart.CreatedAt,
                Items = lines,
                TotalAmount = lines.Sum(x => x.TotalPrice)
            };
        }

        private static void ValidateQuantity(int quantity)
        {
            if (quantity < 1)
            {
                throw new BusinessRuleException("Quantity must be greater than zero.");
            }

            if (quantity > MaxQuantityPerItem)
            {
                throw new BusinessRuleException(
                    $"You can have at most {MaxQuantityPerItem} of one item in your cart.");
            }
        }
    }
}
