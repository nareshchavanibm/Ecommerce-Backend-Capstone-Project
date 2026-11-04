using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReviewService.Api.Clients;
using ReviewService.Api.Controllers;
using ReviewDb = ReviewService.Api.Data.AppDbContext;

namespace Services.IntegrationTests;

public sealed class ReviewApiFactory : WebApplicationFactory<ReviewController>
{
    public StubPurchaseVerifier Purchases { get; } = new();

    public ReviewApiFactory()
    {
        TestSettings.Apply();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            TestSettings.UseInMemoryDatabase<ReviewDb>(services);

            // The OrderService is a separate process; purchases are scripted here.
            services.RemoveAll<IPurchaseVerifier>();
            services.AddSingleton<IPurchaseVerifier>(Purchases);
        });
    }
}

public sealed class StubPurchaseVerifier : IPurchaseVerifier
{
    private readonly HashSet<(int UserId, int ProductId)> _purchases = new();

    public bool Unavailable { get; set; }

    public void Buy(int userId, int productId) => _purchases.Add((userId, productId));

    public Task<bool> HasPurchasedAsync(int userId, int productId, CancellationToken cancellationToken)
    {
        if (Unavailable)
        {
            throw new PurchaseCheckUnavailableException("The order service is not available right now.");
        }

        return Task.FromResult(_purchases.Contains((userId, productId)));
    }
}
