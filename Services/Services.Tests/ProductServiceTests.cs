using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ProductService.Application.DTOs;
using ProductService.Application.DTOs.Categories;
using ProductService.Application.DTOs.Inventory;
using ProductService.Application.DTOs.Products;
using ProductService.Application.Exceptions;
using ProductService.Application.Interfaces.Repositories;
using ProductService.Application.Validators;
using ProductService.Domain.Entities;
using FluentValidation.TestHelper;
using CategorySvc = ProductService.Application.Services.CategoryService;
using InventorySvc = ProductService.Application.Services.InventoryService;
using ProductSvc = ProductService.Application.Services.ProductService;

namespace Services.Tests;

internal sealed class FakeCategoryRepository : ICategoryRepository
{
    private int _nextId = 1;

    public List<Category> Categories { get; } = new();

    public HashSet<int> CategoriesWithProducts { get; } = new();

    public Task<bool> HasProductsAsync(int categoryId) =>
        Task.FromResult(CategoriesWithProducts.Contains(categoryId));

    public Task<Category?> GetByIdAsync(int id) =>
        Task.FromResult(Categories.FirstOrDefault(x => x.Id == id));

    public Task<List<Category>> GetByIdsAsync(List<int> ids) =>
        Task.FromResult(Categories.Where(x => ids.Contains(x.Id)).ToList());

    public Task<List<Category>> GetAllAsync() => Task.FromResult(Categories.ToList());

    public Task<bool> ExistsByNameAsync(string name) =>
        Task.FromResult(Categories.Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)));

    public Task AddAsync(Category category)
    {
        category.Id = _nextId++;
        Categories.Add(category);

        return Task.CompletedTask;
    }

    public Task UpdateAsync(Category category) => Task.CompletedTask;

    public Task DeleteAsync(Category category)
    {
        Categories.Remove(category);

        return Task.CompletedTask;
    }
}

internal sealed class FakeProductRepository : IProductRepository
{
    private int _nextId = 1;

    public List<Product> Products { get; } = new();

    public int GetByIdCalls { get; private set; }

    public Task<Product?> GetByIdAsync(int id)
    {
        GetByIdCalls++;

        return Task.FromResult(Products.FirstOrDefault(x => x.Id == id && x.IsActive));
    }

    public Task<List<Product>> GetByIdsAsync(List<int> ids) =>
        Task.FromResult(Products.Where(x => ids.Contains(x.Id)).ToList());

    public Task<List<Product>> GetAllAsync(
        string? search = null,
        string? category = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        string? sortBy = null,
        string? sortOrder = null) =>
        Task.FromResult(Products.Where(x => x.IsActive).ToList());

    public Task<(List<Product> Products, int TotalItems)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        string? search = null,
        string? category = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        string? sortBy = null,
        string? sortOrder = null)
    {
        var active = Products.Where(x => x.IsActive).ToList();

        return Task.FromResult((
            active.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList(),
            active.Count));
    }

    public Task AddAsync(Product product)
    {
        product.Id = _nextId++;
        Products.Add(product);

        return Task.CompletedTask;
    }

    public Task AddRangeAsync(List<Product> products)
    {
        foreach (var product in products)
        {
            product.Id = _nextId++;
            Products.Add(product);
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(Product product) => Task.CompletedTask;

    public Task UpdateRangeAsync(List<Product> products) => Task.CompletedTask;

    public List<ProductSpecification> Specifications { get; } = new();

    public Task<List<ProductSpecification>> GetSpecificationsAsync(int productId) =>
        Task.FromResult(Specifications.Where(x => x.ProductId == productId).OrderBy(x => x.SortOrder).ToList());

    public Task ReplaceSpecificationsAsync(int productId, List<ProductSpecification> specifications)
    {
        Specifications.RemoveAll(x => x.ProductId == productId);

        foreach (var specification in specifications)
        {
            specification.ProductId = productId;
        }

        Specifications.AddRange(specifications);

        return Task.CompletedTask;
    }

    public Task AddSpecificationsAsync(List<ProductSpecification> specifications)
    {
        Specifications.AddRange(specifications);

        return Task.CompletedTask;
    }

    public Task DeleteAsync(Product product)
    {
        Products.Remove(product);

        return Task.CompletedTask;
    }
}

internal sealed class FakeInventoryRepository : IInventoryRepository
{
    private int _nextId = 1;

    public List<Inventory> Inventories { get; } = new();

    public List<IReadOnlyCollection<StockLineDto>> Reserved { get; } = new();

    public List<IReadOnlyCollection<StockLineDto>> Released { get; } = new();

    public Task<Inventory?> GetByProductIdAsync(int productId) =>
        Task.FromResult(Inventories.FirstOrDefault(x => x.ProductId == productId));

    public Task<List<Inventory>> GetAllAsync() => Task.FromResult(Inventories.ToList());

    public Task<Dictionary<int, int>> GetQuantitiesAsync(IReadOnlyCollection<int> productIds) =>
        Task.FromResult(Inventories
            .Where(x => productIds.Contains(x.ProductId))
            .ToDictionary(x => x.ProductId, x => x.Quantity));

    public Task AddAsync(Inventory inventory)
    {
        inventory.Id = _nextId++;
        Inventories.Add(inventory);

        return Task.CompletedTask;
    }

    public Task AddRangeAsync(List<Inventory> inventories)
    {
        foreach (var inventory in inventories)
        {
            inventory.Id = _nextId++;
            Inventories.Add(inventory);
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(Inventory inventory) => Task.CompletedTask;

    public Task<StockReservationResult> ReserveAsync(IReadOnlyCollection<StockLineDto> lines)
    {
        Reserved.Add(lines);

        return Task.FromResult(StockReservationResult.Ok());
    }

    public Task ReleaseAsync(IReadOnlyCollection<StockLineDto> lines)
    {
        Released.Add(lines);

        return Task.CompletedTask;
    }
}

public class ProductServiceTests
{
    private readonly FakeProductRepository _products = new();

    private readonly FakeCategoryRepository _categories = new();

    private readonly FakeInventoryRepository _inventory = new();

    private readonly IDistributedCache _cache =
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    public ProductServiceTests()
    {
        _categories.Categories.Add(new Category { Id = 1, Name = "Shoes" });
    }

    private ProductSvc CreateService() => new(_products, _categories, _inventory, _cache);

    private static ProductCreateDto NewProduct(int categoryId = 1) => new()
    {
        Name = "Runner",
        Description = "Light running shoe",
        Price = 59.90m,
        ImageUrl = "https://img.example/runner.png",
        CategoryId = categoryId
    };

    [Fact]
    public async Task Create_SavesActiveProductWithCategory()
    {
        var created = await CreateService().CreateAsync(NewProduct());

        var saved = Assert.Single(_products.Products);

        Assert.True(saved.IsActive);
        Assert.Equal(created.Id, saved.Id);
        Assert.Equal("Shoes", created.CategoryName);
        Assert.Equal(59.90m, created.Price);
    }

    [Fact]
    public async Task Create_UnknownCategory_Throws()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => CreateService().CreateAsync(NewProduct(categoryId: 99)));

        Assert.Empty(_products.Products);
    }

    [Fact]
    public async Task GetById_ServesRepeatCallsFromCache()
    {
        var service = CreateService();
        var created = await service.CreateAsync(NewProduct());

        await service.GetByIdAsync(created.Id);
        await service.GetByIdAsync(created.Id);

        Assert.Equal(1, _products.GetByIdCalls);
    }

    [Fact]
    public async Task GetById_Unknown_ReturnsNull()
    {
        Assert.Null(await CreateService().GetByIdAsync(404));
    }

    [Fact]
    public async Task Update_InvalidatesCachedProduct()
    {
        var service = CreateService();
        var created = await service.CreateAsync(NewProduct());

        // Warm the cache, then change the product.
        await service.GetByIdAsync(created.Id);

        var update = NewProduct();
        update.Name = "Runner Pro";
        update.Price = 79m;

        var updated = await service.UpdateAsync(created.Id, update);
        var reread = await service.GetByIdAsync(created.Id);

        Assert.Equal("Runner Pro", updated!.Name);
        Assert.Equal("Runner Pro", reread!.Name);
        Assert.Equal(79m, reread.Price);
    }

    [Fact]
    public async Task Update_UnknownProduct_ReturnsNull()
    {
        Assert.Null(await CreateService().UpdateAsync(404, NewProduct()));
    }

    [Fact]
    public async Task Update_UnknownCategory_Throws()
    {
        var service = CreateService();
        var created = await service.CreateAsync(NewProduct());

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UpdateAsync(created.Id, NewProduct(categoryId: 99)));
    }

    [Fact]
    public async Task Delete_RemovesProductAndClearsCache()
    {
        var service = CreateService();
        var created = await service.CreateAsync(NewProduct());

        await service.GetByIdAsync(created.Id);

        Assert.True(await service.DeleteAsync(created.Id));
        Assert.Null(await service.GetByIdAsync(created.Id));
    }

    [Fact]
    public async Task Delete_Unknown_ReturnsFalse()
    {
        Assert.False(await CreateService().DeleteAsync(404));
    }

    [Fact]
    public async Task GetPaged_ComputesTotalPages()
    {
        var service = CreateService();

        for (var i = 0; i < 5; i++)
        {
            await service.CreateAsync(NewProduct());
        }

        var page = await service.GetPagedAsync(pageNumber: 2, pageSize: 2);

        Assert.Equal(2, page.Items.Count);
        Assert.Equal(5, page.TotalItems);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(2, page.PageNumber);
    }

    [Fact]
    public async Task GetAll_AfterCreate_ReturnsNewProduct()
    {
        var service = CreateService();

        Assert.Empty(await service.GetAllAsync());

        await service.CreateAsync(NewProduct());

        Assert.Single(await service.GetAllAsync());
    }

    [Fact]
    public async Task Import_ValidRowsCreateProductsAndDefaultStock()
    {
        var result = await CreateService().ImportAsync(new List<ProductImportRow>
        {
            new() { RowNumber = 1, Name = "Boot", Price = "100", CategoryName = "Shoes" },
            new() { RowNumber = 2, Name = "Sandal", Price = "20.50", CategoryId = "1" }
        });

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(2, result.Inserted);
        Assert.Equal(0, result.Errors);
        Assert.Equal(2, _products.Products.Count);
        Assert.All(_inventory.Inventories, x => Assert.Equal(10, x.Quantity));
    }

    [Fact]
    public async Task Import_CreatesMissingCategoryByName()
    {
        await CreateService().ImportAsync(new List<ProductImportRow>
        {
            new() { RowNumber = 1, Name = "Hat", Price = "9", CategoryName = "Accessories" }
        });

        Assert.Contains(_categories.Categories, x => x.Name == "Accessories");
    }

    [Theory]
    [InlineData("", "10", "Shoes", "Name is required.")]
    [InlineData("Boot", "0", "Shoes", "Price must be a number greater than 0.")]
    [InlineData("Boot", "abc", "Shoes", "Price must be a number greater than 0.")]
    [InlineData("Boot", "10", "", "CategoryName or a valid CategoryId is required.")]
    public async Task Import_InvalidRow_IsReportedAndSkipped(
        string name,
        string price,
        string categoryName,
        string expectedError)
    {
        var result = await CreateService().ImportAsync(new List<ProductImportRow>
        {
            new() { RowNumber = 7, Name = name, Price = price, CategoryName = categoryName }
        });

        Assert.Equal(1, result.Errors);
        Assert.Equal(0, result.Inserted);
        Assert.Empty(_products.Products);
        Assert.Equal($"Row 7: {expectedError}", Assert.Single(result.ErrorDetails));
    }

    [Fact]
    public async Task Import_BadRowDoesNotBlockGoodRows()
    {
        var result = await CreateService().ImportAsync(new List<ProductImportRow>
        {
            new() { RowNumber = 1, Name = "", Price = "5", CategoryName = "Shoes" },
            new() { RowNumber = 2, Name = "Boot", Price = "5", CategoryName = "Shoes" }
        });

        Assert.Equal(1, result.Inserted);
        Assert.Equal(1, result.Errors);
    }

    [Fact]
    public async Task BulkUpdate_ChangesOnlyProvidedFields()
    {
        var service = CreateService();
        var created = await service.CreateAsync(NewProduct());

        var updated = await service.BulkUpdateAsync(new List<BulkUpdateProductRequest>
        {
            new() { Id = created.Id, Price = 10m },
            new() { Id = 999, Price = 1m }
        });

        var single = Assert.Single(updated);

        Assert.Equal(10m, single.Price);
        Assert.Equal("Runner", single.Name);
    }

    [Fact]
    public async Task BulkUpdate_UnknownCategoryWithoutName_Throws()
    {
        var service = CreateService();
        var created = await service.CreateAsync(NewProduct());

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.BulkUpdateAsync(new List<BulkUpdateProductRequest>
            {
                new() { Id = created.Id, CategoryId = 99 }
            }));
    }
}

public class CategoryServiceTests
{
    private readonly FakeCategoryRepository _categories = new();

    private CategorySvc CreateService() => new(_categories);

    [Fact]
    public async Task Create_SavesCategory()
    {
        var created = await CreateService().CreateAsync(
            new CategoryCreateDto { Name = "Shoes", Description = "Footwear" });

        Assert.Equal("Shoes", created.Name);
        Assert.Single(_categories.Categories);
    }

    [Fact]
    public async Task Create_DuplicateName_Throws()
    {
        var service = CreateService();

        await service.CreateAsync(new CategoryCreateDto { Name = "Shoes" });

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.CreateAsync(new CategoryCreateDto { Name = "shoes" }));
    }

    [Fact]
    public async Task Update_ToAnotherCategorysName_Throws()
    {
        var service = CreateService();

        await service.CreateAsync(new CategoryCreateDto { Name = "Shoes" });
        var hats = await service.CreateAsync(new CategoryCreateDto { Name = "Hats" });

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UpdateAsync(hats.Id, new CategoryCreateDto { Name = "Shoes" }));
    }

    [Fact]
    public async Task Delete_CategoryWithProducts_Throws()
    {
        var created = await CreateService().CreateAsync(new CategoryCreateDto { Name = "Shoes" });

        _categories.CategoriesWithProducts.Add(created.Id);

        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().DeleteAsync(created.Id));

        Assert.Single(_categories.Categories);
    }

    [Fact]
    public async Task Delete_EmptyCategory_RemovesIt()
    {
        var created = await CreateService().CreateAsync(new CategoryCreateDto { Name = "Shoes" });

        Assert.True(await CreateService().DeleteAsync(created.Id));
        Assert.Empty(_categories.Categories);
    }

    [Fact]
    public async Task GetById_Unknown_ReturnsNull()
    {
        Assert.Null(await CreateService().GetByIdAsync(404));
    }
}

public class InventoryServiceTests
{
    private sealed class RecordingNotifier : ProductService.Application.Interfaces.Services.IRestockNotifier
    {
        public List<int> Restocked { get; } = new();

        public Task ProductRestockedAsync(int productId)
        {
            Restocked.Add(productId);

            return Task.CompletedTask;
        }
    }

    private readonly FakeInventoryRepository _inventory = new();

    private readonly RecordingNotifier _notifier = new();

    private InventorySvc CreateService() => new(_inventory, _notifier);

    [Theory]
    [InlineData(0, 5, true)]
    [InlineData(3, 8, false)]
    [InlineData(0, 0, false)]
    public async Task Update_NotifiesOnlyWhenOutOfStockProductIsRestocked(int before, int after, bool expected)
    {
        var service = CreateService();

        await service.CreateAsync(1, new InventoryUpdateDto { Quantity = before, ReorderLevel = 1 });
        await service.UpdateAsync(1, new InventoryUpdateDto { Quantity = after, ReorderLevel = 1 });

        Assert.Equal(expected, _notifier.Restocked.Contains(1));
    }

    [Fact]
    public async Task Create_NegativeQuantity_Throws()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => CreateService().CreateAsync(1, new InventoryUpdateDto { Quantity = -1, ReorderLevel = 0 }));
    }

    [Fact]
    public async Task Create_ExistingInventory_Throws()
    {
        var service = CreateService();

        await service.CreateAsync(1, new InventoryUpdateDto { Quantity = 5, ReorderLevel = 1 });

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.CreateAsync(1, new InventoryUpdateDto { Quantity = 5, ReorderLevel = 1 }));
    }

    [Theory]
    [InlineData(5, 5, true)]
    [InlineData(3, 5, true)]
    [InlineData(6, 5, false)]
    public async Task Create_FlagsLowStock(int quantity, int reorderLevel, bool expectedLow)
    {
        var created = await CreateService().CreateAsync(
            1,
            new InventoryUpdateDto { Quantity = quantity, ReorderLevel = reorderLevel });

        Assert.Equal(expectedLow, created.IsLowStock);
    }

    [Fact]
    public async Task Update_UnknownProduct_ReturnsNull()
    {
        Assert.Null(await CreateService().UpdateAsync(
            404,
            new InventoryUpdateDto { Quantity = 1, ReorderLevel = 1 }));
    }

    [Fact]
    public async Task Update_ChangesQuantityAndReorderLevel()
    {
        var service = CreateService();

        await service.CreateAsync(1, new InventoryUpdateDto { Quantity = 5, ReorderLevel = 1 });

        var updated = await service.UpdateAsync(1, new InventoryUpdateDto { Quantity = 20, ReorderLevel = 4 });

        Assert.Equal(20, updated!.Quantity);
        Assert.Equal(4, updated.ReorderLevel);
    }

    [Fact]
    public async Task Reserve_MergesRepeatedProductsIntoOneLine()
    {
        await CreateService().ReserveAsync(new StockAdjustmentRequest
        {
            Items =
            {
                new StockLineDto { ProductId = 1, Quantity = 2 },
                new StockLineDto { ProductId = 1, Quantity = 3 },
                new StockLineDto { ProductId = 2, Quantity = 1 }
            }
        });

        var lines = Assert.Single(_inventory.Reserved);

        Assert.Equal(2, lines.Count);
        Assert.Equal(5, lines.Single(x => x.ProductId == 1).Quantity);
    }
}

public class ProductValidatorTests
{
    private readonly CategoryCreateDtoValidator _category = new();

    [Fact]
    public void Category_ValidName_Passes()
    {
        _category.TestValidate(new CategoryCreateDto { Name = "Shoes" }).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Category_BlankName_Fails(string name)
    {
        _category.TestValidate(new CategoryCreateDto { Name = name }).ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Category_NameOrDescriptionTooLong_Fails()
    {
        var result = _category.TestValidate(new CategoryCreateDto
        {
            Name = new string('a', 101),
            Description = new string('b', 501)
        });

        result.ShouldHaveValidationErrorFor(x => x.Name);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    private readonly ProductCreateDtoValidator _validator = new();

    private static ProductCreateDto Valid() => new()
    {
        Name = "Runner",
        Description = "Light running shoe",
        Price = 10m,
        CategoryId = 1
    };

    [Fact]
    public void ValidProduct_Passes()
    {
        _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyName_Fails()
    {
        var dto = Valid();
        dto.Name = "";

        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void NameLongerThan100_Fails()
    {
        var dto = Valid();
        dto.Name = new string('a', 101);

        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void NonPositivePrice_Fails(double price)
    {
        var dto = Valid();
        dto.Price = (decimal)price;

        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.Price);
    }

    [Fact]
    public void MissingCategory_Fails()
    {
        var dto = Valid();
        dto.CategoryId = 0;

        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.CategoryId);
    }

    [Fact]
    public void ImageUrlLongerThan500_Fails()
    {
        var dto = Valid();
        dto.ImageUrl = new string('a', 501);

        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.ImageUrl);
    }
}
