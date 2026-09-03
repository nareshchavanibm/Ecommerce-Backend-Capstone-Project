using ECommerce.Application.DTOs.Checkout;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Interfaces.Repositories;
using ECommerce.Domain.Entities;

namespace ECommerce.Application.Services;

public class CheckoutService(
    ICartRepository cartRepository,
    IOrderRepository orderRepository,
    IInventoryRepository inventoryRepository,
    ITransactionService transactionService)
    : ICheckoutService
{
    public async Task<CheckoutDto> CheckoutAsync(int userId)
    {
        CheckoutDto? result = null;

        await transactionService.ExecuteInTransactionAsync(async () =>
        {
            // 1. Get user's cart
            var cart =
                await cartRepository.GetByUserIdAsync(userId);

            // 2. Validate cart
            if (cart == null || cart.CartItems.Count == 0)
            {
                throw new ArgumentException(
                    "Cart is empty.");
            }

            // 3. Create order
            var order = new Order
            {
                UserId = userId,
                OrderDate = DateTime.UtcNow,
                Status = "Pending"
            };

            // 4. Process every cart item
            foreach (var cartItem in cart.CartItems)
            {
                if (cartItem.Quantity <= 0)
                {
                    throw new ArgumentException(
                        "Cart contains an invalid quantity.");
                }

                var product = cartItem.Product;

                // Product should already be loaded
                // by CartRepository
                if (product == null)
                {
                    throw new ArgumentException(
                        $"Product {cartItem.ProductId} " +
                        "was not found.");
                }

                // 5. Get inventory
                var inventory =
                    await inventoryRepository
                        .GetByProductIdAsync(product.Id);

                if (inventory == null)
                {
                    throw new ArgumentException(
                        $"Inventory was not found " +
                        $"for {product.Name}.");
                }

                // 6. Check stock
                if (inventory.Quantity < cartItem.Quantity)
                {
                    throw new ArgumentException(
                        $"Insufficient stock for " +
                        $"{product.Name}. " +
                        $"Available: {inventory.Quantity}.");
                }

                // 7. Reduce inventory
                inventory.Quantity -= cartItem.Quantity;

                await inventoryRepository
                    .UpdateAsync(inventory);

                // 8. Create OrderItem
                var orderItem = new OrderItem
                {
                    ProductId = product.Id,
                    Quantity = cartItem.Quantity,
                    UnitPrice = product.Price,
                    TotalPrice =
                        product.Price * cartItem.Quantity
                };

                order.OrderItems.Add(orderItem);
            }

            // 9. Calculate order total
            order.TotalAmount =
                order.OrderItems.Sum(x => x.TotalPrice);

            // 10. Create order
            await orderRepository.AddAsync(order);

            // 11. Clear cart
            cart.CartItems.Clear();

            await cartRepository.UpdateAsync(cart);

            // 12. Save everything
            // TransactionService will commit after this operation
            //await orderRepository.SaveChangesAsync();

            result = new CheckoutDto
            {
                TotalAmount = order.TotalAmount
            };
        });

        return result!;
    }
}