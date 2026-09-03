using ECommerce.Application.DTOs.Checkout;

namespace ECommerce.Application.Interfaces;

public interface ICheckoutService
{
    Task<CheckoutDto> CheckoutAsync(int userId);
}