using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Caching.Distributed;
using OrderService.Api.Controllers;
using OrderService.Application.Interfaces.Clients;
using ProductService.Api.Controllers;
using UserService.Api.Controllers;
using UserService.Infrastructure.Data;
using OrderDb = OrderService.Infrastructure.Data.AppDbContext;
using ProductDb = ProductService.Infrastructure.Data.AppDbContext;

namespace Services.IntegrationTests;

public sealed class UserApiFactory : WebApplicationFactory<AuthController>
{
    public UserApiFactory()
    {
        TestSettings.Apply();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services => TestSettings.UseInMemoryDatabase<AppDbContext>(services));
    }
}

public sealed class ProductApiFactory : WebApplicationFactory<ProductController>
{
    public ProductApiFactory()
    {
        TestSettings.Apply();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            TestSettings.UseInMemoryDatabase<ProductDb>(services);

            // Redis is not available in tests.
            services.RemoveAll<IDistributedCache>();
            services.AddDistributedMemoryCache();
        });
    }
}

public sealed class OrderApiFactory : WebApplicationFactory<OrderController>
{
    public StubCatalog Catalog { get; } = new();

    public StubStock Stock { get; } = new();

    public OrderApiFactory()
    {
        TestSettings.Apply();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            TestSettings.UseInMemoryDatabase<OrderDb>(services);

            // The ProductService is a separate process; its answers are scripted here.
            services.RemoveAll<IProductCatalog>();
            services.RemoveAll<IStockService>();
            services.AddSingleton<IProductCatalog>(Catalog);
            services.AddSingleton<IStockService>(Stock);
        });
    }
}

public sealed class StubCatalog : IProductCatalog
{
    private readonly Dictionary<int, CatalogProduct> _products = new();

    public void Add(int id, string name, decimal price, bool isActive = true) =>
        _products[id] = new CatalogProduct(id, name, price, isActive);

    public Task<IReadOnlyDictionary<int, CatalogProduct>> GetProductsAsync(
        IEnumerable<int> productIds,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<int, CatalogProduct> found = productIds
            .Distinct()
            .Where(_products.ContainsKey)
            .ToDictionary(id => id, id => _products[id]);

        return Task.FromResult(found);
    }
}

public sealed class StubStock : IStockService
{
    public List<IReadOnlyCollection<StockLine>> Reserved { get; } = new();

    public List<IReadOnlyCollection<StockLine>> Released { get; } = new();

    // When set, reservation fails with this message (HTTP 400 from the API).
    public string? RejectWith { get; set; }

    public Task ReserveAsync(IReadOnlyCollection<StockLine> lines)
    {
        if (RejectWith != null)
        {
            throw new OrderService.Application.Exceptions.BusinessRuleException(RejectWith);
        }

        Reserved.Add(lines);

        return Task.CompletedTask;
    }

    public Task ReleaseAsync(IReadOnlyCollection<StockLine> lines)
    {
        Released.Add(lines);

        return Task.CompletedTask;
    }
}
