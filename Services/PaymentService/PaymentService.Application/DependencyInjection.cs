using Microsoft.Extensions.DependencyInjection;
using PaymentService.Application.Interfaces;
using PaymentService.Application.Services;

namespace PaymentService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentApplication(
        this IServiceCollection services)
    {
        services.AddScoped<IPaymentService, Services.PaymentService>();

        // Swap for a real provider implementation in production.
        services.AddSingleton<IPaymentGateway, MockPaymentGateway>();

        return services;
    }
}
