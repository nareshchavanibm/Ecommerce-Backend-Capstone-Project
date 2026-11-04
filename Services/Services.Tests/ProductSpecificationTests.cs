using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ProductService.Application.DTOs.Products;
using ProductService.Application.Exceptions;
using ProductService.Application.Services;
using ProductService.Domain.Entities;
using ProductSvc = ProductService.Application.Services.ProductService;

namespace Services.Tests;

public class ProductSpecificationTests
{
    private readonly FakeProductRepository _products = new();

    private readonly FakeCategoryRepository _categories = new();

    private readonly FakeInventoryRepository _inventory = new();

    private readonly IDistributedCache _cache =
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    public ProductSpecificationTests()
    {
        _categories.Categories.Add(new Category { Id = 1, Name = "Phones" });
    }

    private ProductSvc CreateService() => new(_products, _categories, _inventory, _cache);

    private static ProductSpecificationDto Spec(string? group, string name, string value) =>
        new() { Group = group, Name = name, Value = value };

    private static ProductCreateDto NewProduct(params ProductSpecificationDto[] specs) => new()
    {
        Name = "Phone X",
        Description = "A phone",
        Price = 999m,
        CategoryId = 1,
        Specifications = specs.ToList()
    };

    [Fact]
    public void Normalize_TrimsDefaultsGroupAndKeepsOrder()
    {
        var error = SpecificationNormalizer.TryNormalize(
            [Spec(null, " Brand ", " Acme "), Spec("Display", "Size", "6.5 inch")],
            out var result);

        Assert.Null(error);
        Assert.Equal(["General", "Display"], result.Select(x => x.Group));
        Assert.Equal(["Brand", "Size"], result.Select(x => x.Name));
        Assert.Equal("Acme", result[0].Value);
        Assert.Equal([0, 1], result.Select(x => x.SortOrder));
    }

    [Fact]
    public void Normalize_DropsBlankRows()
    {
        var error = SpecificationNormalizer.TryNormalize(
            [Spec(null, "", ""), Spec("  ", "  ", "  "), Spec(null, "RAM", "8 GB")],
            out var result);

        Assert.Null(error);
        Assert.Single(result);
    }

    [Theory]
    [InlineData("", "value", "name")]
    [InlineData("Name", "", "needs a value")]
    public void Normalize_IncompleteRow_ReturnsError(string name, string value, string expected)
    {
        var error = SpecificationNormalizer.TryNormalize([Spec(null, name, value)], out _);

        Assert.Contains(expected, error);
    }

    [Fact]
    public void Normalize_DuplicateInSameGroup_ReturnsError()
    {
        var error = SpecificationNormalizer.TryNormalize(
            [Spec("Display", "Size", "6"), Spec("display", "size", "7")],
            out _);

        Assert.Contains("twice", error);
    }

    [Fact]
    public void Normalize_SameNameInDifferentGroups_IsAllowed()
    {
        var error = SpecificationNormalizer.TryNormalize(
            [Spec("Front", "Camera", "8 MP"), Spec("Rear", "Camera", "48 MP")],
            out var result);

        Assert.Null(error);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Normalize_TooLongValueOrTooMany_ReturnsError()
    {
        Assert.NotNull(SpecificationNormalizer.TryNormalize([Spec(null, "A", new string('x', 501))], out _));

        var many = Enumerable.Range(0, 61).Select(i => Spec(null, $"Name {i}", "v"));

        Assert.NotNull(SpecificationNormalizer.TryNormalize(many, out _));
    }

    [Fact]
    public async Task Create_StoresSpecificationsAndReturnsThem()
    {
        var created = await CreateService().CreateAsync(NewProduct(Spec("Display", "Size", "6.5 inch")));

        Assert.Single(_products.Specifications);
        Assert.Equal(created.Id, _products.Specifications[0].ProductId);
        Assert.Equal("Size", Assert.Single(created.Specifications).Name);
    }

    [Fact]
    public async Task Create_InvalidSpecification_SavesNothing()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => CreateService().CreateAsync(NewProduct(Spec(null, "Brand", ""))));

        Assert.Empty(_products.Products);
    }

    [Fact]
    public async Task Update_NullSpecifications_KeepsExistingOnes()
    {
        var service = CreateService();
        var created = await service.CreateAsync(NewProduct(Spec(null, "Brand", "Acme")));

        var update = NewProduct();
        update.Specifications = null;

        var updated = await service.UpdateAsync(created.Id, update);

        Assert.Equal("Brand", Assert.Single(updated!.Specifications).Name);
    }

    [Fact]
    public async Task Update_ReplacesAndClearsSpecifications()
    {
        var service = CreateService();
        var created = await service.CreateAsync(NewProduct(Spec(null, "Brand", "Acme")));

        var replaced = await service.UpdateAsync(created.Id, NewProduct(Spec(null, "RAM", "8 GB")));

        Assert.Equal("RAM", Assert.Single(replaced!.Specifications).Name);
        Assert.Equal("RAM", Assert.Single((await service.GetByIdAsync(created.Id))!.Specifications).Name);

        await service.UpdateAsync(created.Id, NewProduct());

        Assert.Empty((await service.GetByIdAsync(created.Id))!.Specifications);
    }

    [Fact]
    public async Task Import_StoresSpecificationsPerProduct()
    {
        var result = await CreateService().ImportAsync(
        [
            new ProductImportRow
            {
                RowNumber = 2, Name = "A", Price = "10", CategoryName = "Phones",
                Specifications = [Spec("Display", "Size", "6 inch"), Spec(null, "Brand", "Acme")]
            },
            new ProductImportRow { RowNumber = 3, Name = "B", Price = "20", CategoryName = "Phones" }
        ]);

        Assert.Equal(2, result.Inserted);
        Assert.Equal(2, _products.Specifications.Count);
        Assert.All(_products.Specifications, x => Assert.Equal(_products.Products[0].Id, x.ProductId));
    }

    [Fact]
    public async Task Import_BadSpecificationRejectsOnlyThatRow()
    {
        var result = await CreateService().ImportAsync(
        [
            new ProductImportRow
            {
                RowNumber = 2, Name = "A", Price = "10", CategoryName = "Phones",
                Specifications = [Spec("G", "N", "1"), Spec("G", "N", "2")]
            },
            new ProductImportRow { RowNumber = 3, Name = "B", Price = "20", CategoryName = "Phones" }
        ]);

        Assert.Equal(1, result.Inserted);
        Assert.Equal(1, result.Errors);
        Assert.Contains("Row 2", result.ErrorDetails[0]);
        Assert.Empty(_products.Specifications);
    }
}
