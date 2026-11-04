using FluentValidation.TestHelper;
using Microsoft.Extensions.Logging.Abstractions;
using OrderService.Application.DTOs.Orders;
using OrderService.Application.Interfaces.Clients;
using ProductService.Application.DTOs.Promotions;
using ProductService.Application.Interfaces.Repositories;
using ProductService.Application.Validators;
using ProductService.Domain.Entities;
using OrderSvc = OrderService.Application.Services.OrderService;
using PromotionSvc = ProductService.Application.Services.PromotionService;

namespace Services.Tests;

internal sealed class FakePromotionRepository : IPromotionRepository
{
    private int _nextId = 1;

    public List<Promotion> Promotions { get; } = new();

    public Task<Promotion?> GetByIdAsync(int id) =>
        Task.FromResult(Promotions.FirstOrDefault(x => x.Id == id));

    public Task<List<Promotion>> GetAllAsync() =>
        Task.FromResult(Promotions.OrderBy(x => x.DisplayOrder).ToList());

    public Task<List<Promotion>> GetActiveAsync(DateTime utcNow) =>
        Task.FromResult(Promotions
            .Where(x => x.IsActive
                && (x.StartsAt == null || x.StartsAt <= utcNow)
                && (x.EndsAt == null || x.EndsAt >= utcNow))
            .OrderBy(x => x.DisplayOrder)
            .ToList());

    public Task AddAsync(Promotion promotion)
    {
        promotion.Id = _nextId++;
        Promotions.Add(promotion);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Promotion promotion) => Task.CompletedTask;

    public Task DeleteAsync(Promotion promotion)
    {
        Promotions.Remove(promotion);
        return Task.CompletedTask;
    }
}

public class PromotionTests
{
    private readonly FakePromotionRepository _repository = new();
    private readonly PromotionSvc _service;
    private readonly PromotionCreateDtoValidator _validator = new();

    public PromotionTests()
    {
        _service = new PromotionSvc(_repository);
    }

    private static PromotionCreateDto Request(Action<PromotionCreateDto>? tweak = null)
    {
        var dto = new PromotionCreateDto { Type = "Offer", Title = "Festive sale", LinkUrl = "/products" };
        tweak?.Invoke(dto);
        return dto;
    }

    [Fact]
    public async Task Only_active_promotions_inside_their_window_are_public()
    {
        await _service.CreateAsync(Request(d => d.Title = "Live"));
        await _service.CreateAsync(Request(d => { d.Title = "Hidden"; d.IsActive = false; }));
        await _service.CreateAsync(Request(d => { d.Title = "Expired"; d.EndsAt = DateTime.UtcNow.AddDays(-1); }));
        await _service.CreateAsync(Request(d => { d.Title = "Upcoming"; d.StartsAt = DateTime.UtcNow.AddDays(1); }));

        var active = await _service.GetActiveAsync();

        Assert.Equal("Live", Assert.Single(active).Title);
        Assert.Equal(4, (await _service.GetAllAsync()).Count);
    }

    [Fact]
    public async Task Update_and_delete_report_missing_promotions()
    {
        Assert.Null(await _service.UpdateAsync(99, Request()));
        Assert.False(await _service.DeleteAsync(99));
    }

    [Fact]
    public void Valid_request_passes_validation() =>
        _validator.TestValidate(Request()).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("//evil.example.com")]
    [InlineData("products")]
    public void Unsafe_links_are_rejected(string link) =>
        _validator.TestValidate(Request(d => d.LinkUrl = link)).ShouldHaveValidationErrorFor(x => x.LinkUrl);

    [Theory]
    [InlineData("/products?categoryId=2")]
    [InlineData("https://example.com/sale")]
    public void Safe_links_are_accepted(string link) =>
        _validator.TestValidate(Request(d => d.LinkUrl = link)).ShouldNotHaveValidationErrorFor(x => x.LinkUrl);

    [Fact]
    public void Image_must_be_an_http_url() =>
        _validator.TestValidate(Request(d => d.ImageUrl = "javascript:alert(1)"))
            .ShouldHaveValidationErrorFor(x => x.ImageUrl);

    [Fact]
    public void Unknown_type_and_reversed_dates_are_rejected()
    {
        _validator.TestValidate(Request(d => d.Type = "Popup")).ShouldHaveValidationErrorFor(x => x.Type);

        _validator.TestValidate(Request(d =>
        {
            d.StartsAt = DateTime.UtcNow.AddDays(2);
            d.EndsAt = DateTime.UtcNow.AddDays(1);
        })).ShouldHaveValidationErrorFor(x => x.EndsAt);
    }
}

public class TrendingProductsTests
{
    private readonly FakeOrderRepository _orders = new();
    private readonly OrderSvc _service;

    public TrendingProductsTests()
    {
        var catalog = new FakeProductCatalog();
        catalog.Products[1] = new CatalogProduct(1, "Trail Shoe", 100m, true);
        catalog.Products[2] = new CatalogProduct(2, "Road Shoe", 250m, true);
        catalog.Products[3] = new CatalogProduct(3, "Sandal", 50m, true);

        _service = new OrderSvc(
            _orders,
            new FakeOrderItemRepository(_orders),
            catalog,
            new FakeStockService(),
            NullLogger<OrderSvc>.Instance);
    }

    private Task<OrderResponseDto> Order(params (int productId, int quantity)[] lines) =>
        _service.CreateOrderAsync(new OrderCreateDto
        {
            UserId = 7,
            Items = lines.Select(l => new OrderItemCreateDto { ProductId = l.productId, Quantity = l.quantity }).ToList()
        });

    [Fact]
    public async Task Products_are_ranked_by_units_sold()
    {
        await Order((1, 1), (2, 5));
        await Order((1, 2), (3, 1));

        var trending = await _service.GetTrendingProductsAsync(30, 10);

        Assert.Equal(new[] { 2, 1, 3 }, trending.Select(t => t.ProductId));
        Assert.Equal(5, trending[0].UnitsSold);
        Assert.Equal(3, trending[1].UnitsSold);
    }

    [Fact]
    public async Task Cancelled_orders_do_not_count()
    {
        var cancelled = await Order((3, 10));
        await Order((1, 1));
        await _service.CancelOrderAsync(cancelled.OrderNumber, 7, false);

        var trending = await _service.GetTrendingProductsAsync(30, 10);

        Assert.Equal(1, Assert.Single(trending).ProductId);
    }

    [Fact]
    public async Task Result_is_limited_to_the_requested_count()
    {
        await Order((1, 3), (2, 2), (3, 1));

        Assert.Equal(2, (await _service.GetTrendingProductsAsync(30, 2)).Count);
    }
}
