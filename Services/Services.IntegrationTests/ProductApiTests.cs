using System.Net;
using System.Net.Http.Json;
using ProductService.Application.DTOs.Categories;
using ProductService.Application.DTOs.Products;

namespace Services.IntegrationTests;

public class ProductApiTests(ProductApiFactory factory) : IClassFixture<ProductApiFactory>
{
    private HttpClient Anonymous() => factory.CreateClient();

    private HttpClient Admin() => factory.CreateClient().AsAdmin();

    private HttpClient Customer() => factory.CreateClient().AsCustomer();

    private async Task<CategoryResponseDto> CreateCategoryAsync()
    {
        var response = await Admin().PostAsJsonAsync(
            "/api/category",
            new CategoryCreateDto { Name = "Cat-" + Guid.NewGuid().ToString("N"), Description = "Test category" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<CategoryResponseDto>())!;
    }

    private static ProductCreateDto NewProduct(int categoryId, string? name = null, decimal price = 25m) => new()
    {
        Name = name ?? "Product-" + Guid.NewGuid().ToString("N"),
        Description = "A test product",
        Price = price,
        CategoryId = categoryId
    };

    private async Task<ProductResponseDto> CreateProductAsync(
        int categoryId,
        string? name = null,
        decimal price = 25m)
    {
        var response = await Admin().PostAsJsonAsync("/api/product", NewProduct(categoryId, name, price));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<ProductResponseDto>())!;
    }

    [Fact]
    public async Task List_IsPublic()
    {
        var response = await Anonymous().GetAsync("/api/product");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutToken_ReturnsUnauthorized()
    {
        var response = await Anonymous().PostAsJsonAsync("/api/product", NewProduct(1));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsCustomer_ReturnsForbidden()
    {
        var response = await Customer().PostAsJsonAsync("/api/product", NewProduct(1));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_AsAdmin_ReturnsCreatedAndLocation()
    {
        var category = await CreateCategoryAsync();

        var response = await Admin().PostAsJsonAsync("/api/product", NewProduct(category.Id, "Runner", 59.9m));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<ProductResponseDto>();

        Assert.Equal("Runner", created!.Name);
        Assert.Equal(category.Name, created.CategoryName);
        Assert.EndsWith($"/api/Product/{created.Id}", response.Headers.Location!.AbsolutePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_InvalidBody_ReturnsBadRequest()
    {
        var response = await Admin().PostAsJsonAsync("/api/product", new ProductCreateDto
        {
            Name = "",
            Description = "",
            Price = -1,
            CategoryId = 0
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ReturnsCreatedProduct()
    {
        var category = await CreateCategoryAsync();
        var created = await CreateProductAsync(category.Id, "Findable");

        var response = await Anonymous().GetAsync($"/api/product/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ProductResponseDto>();

        Assert.Equal("Findable", body!.Name);
    }

    [Fact]
    public async Task GetById_Unknown_ReturnsNotFound()
    {
        var response = await Anonymous().GetAsync("/api/product/987654");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_NonPositiveId_ReturnsBadRequest()
    {
        var response = await Anonymous().GetAsync("/api/product/0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_AsAdmin_ChangesProduct()
    {
        var category = await CreateCategoryAsync();
        var created = await CreateProductAsync(category.Id, "Old name", 10m);

        var response = await Admin().PutAsJsonAsync(
            $"/api/product/{created.Id}",
            NewProduct(category.Id, "New name", 15m));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var reread = await Anonymous().GetFromJsonAsync<ProductResponseDto>($"/api/product/{created.Id}");

        Assert.Equal("New name", reread!.Name);
        Assert.Equal(15m, reread.Price);
    }

    [Fact]
    public async Task Update_AsCustomer_ReturnsForbidden()
    {
        var response = await Customer().PutAsJsonAsync("/api/product/1", NewProduct(1));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_Unknown_ReturnsNotFound()
    {
        var category = await CreateCategoryAsync();

        var response = await Admin().PutAsJsonAsync("/api/product/987654", NewProduct(category.Id));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsAdmin_RemovesProduct()
    {
        var category = await CreateCategoryAsync();
        var created = await CreateProductAsync(category.Id);

        var response = await Admin().DeleteAsync($"/api/product/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Anonymous().GetAsync($"/api/product/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Delete_AsCustomer_ReturnsForbidden()
    {
        var response = await Customer().DeleteAsync("/api/product/1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Unknown_ReturnsNotFound()
    {
        var response = await Admin().DeleteAsync("/api/product/987654");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Search_FiltersByCategoryName()
    {
        var first = await CreateCategoryAsync();
        var second = await CreateCategoryAsync();

        await CreateProductAsync(first.Id);
        await CreateProductAsync(first.Id);
        await CreateProductAsync(second.Id);

        var page = await Anonymous().GetFromJsonAsync<PagedProductResponseDto>(
            $"/api/product/search?category={Uri.EscapeDataString(first.Name)}");

        Assert.Equal(2, page!.TotalItems);
        Assert.All(page.Items, x => Assert.Equal(first.Name, x.CategoryName));
    }

    [Fact]
    public async Task Search_FiltersByPriceRange()
    {
        var category = await CreateCategoryAsync();

        await CreateProductAsync(category.Id, price: 5m);
        await CreateProductAsync(category.Id, price: 50m);
        await CreateProductAsync(category.Id, price: 500m);

        var page = await Anonymous().GetFromJsonAsync<PagedProductResponseDto>(
            $"/api/product/search?category={Uri.EscapeDataString(category.Name)}&minPrice=10&maxPrice=100");

        Assert.Equal(50m, Assert.Single(page!.Items).Price);
    }

    [Fact]
    public async Task Search_PagesAndSortsByPrice()
    {
        var category = await CreateCategoryAsync();

        foreach (var price in new[] { 30m, 10m, 20m, 40m, 50m })
        {
            await CreateProductAsync(category.Id, price: price);
        }

        var page = await Anonymous().GetFromJsonAsync<PagedProductResponseDto>(
            $"/api/product/search?category={Uri.EscapeDataString(category.Name)}&sortBy=price&sortOrder=desc&pageNumber=1&pageSize=2");

        Assert.Equal(5, page!.TotalItems);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(new[] { 50m, 40m }, page.Items.Select(x => x.Price).ToArray());
    }

    [Fact]
    public async Task Search_ByKeyword_MatchesNameOrDescription()
    {
        var category = await CreateCategoryAsync();
        var token = "zq" + Guid.NewGuid().ToString("N")[..8];

        await CreateProductAsync(category.Id, name: "Shoe " + token);
        await CreateProductAsync(category.Id);

        var page = await Anonymous().GetFromJsonAsync<PagedProductResponseDto>(
            $"/api/product/search?search={token}");

        Assert.Single(page!.Items);
    }

    [Theory]
    [InlineData("pageNumber=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("minPrice=-1")]
    [InlineData("minPrice=10&maxPrice=5")]
    [InlineData("sortBy=colour")]
    [InlineData("sortOrder=sideways")]
    public async Task Search_InvalidQuery_ReturnsBadRequest(string query)
    {
        var response = await Anonymous().GetAsync("/api/product/search?" + query);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_SearchLongerThan100_ReturnsBadRequest()
    {
        var response = await Anonymous().GetAsync("/api/product/search?search=" + new string('a', 101));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Categories_AreListedPublicly_ButCreatedOnlyByAdmin()
    {
        var anonymousCreate = await Anonymous().PostAsJsonAsync(
            "/api/category",
            new CategoryCreateDto { Name = "Nope" });

        var customerCreate = await Customer().PostAsJsonAsync(
            "/api/category",
            new CategoryCreateDto { Name = "Nope" });

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousCreate.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, customerCreate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Anonymous().GetAsync("/api/category")).StatusCode);
    }

    private static async Task<string> MessageAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("message").GetString()!;

    [Fact]
    public async Task Create_InvalidBody_ExplainsWhatIsWrongInMessage()
    {
        var response = await Admin().PostAsJsonAsync("/api/product", new ProductCreateDto
        {
            Name = "",
            Description = "d",
            Price = 5,
            CategoryId = 1
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(await MessageAsync(response)));
    }

    [Fact]
    public async Task Create_UnknownCategory_ReturnsBadRequestInsteadOfServerError()
    {
        var response = await Admin().PostAsJsonAsync("/api/product", NewProduct(categoryId: 987654));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Category not found.", await MessageAsync(response));
    }

    [Fact]
    public async Task Category_BlankName_ReturnsBadRequest()
    {
        var response = await Admin().PostAsJsonAsync("/api/category", new CategoryCreateDto { Name = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Category_DuplicateName_ReturnsBadRequest()
    {
        var category = await CreateCategoryAsync();

        var response = await Admin().PostAsJsonAsync(
            "/api/category",
            new CategoryCreateDto { Name = category.Name });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("already exists", await MessageAsync(response));
    }

    [Fact]
    public async Task Category_DeleteWithProducts_ReturnsBadRequest()
    {
        var category = await CreateCategoryAsync();

        await CreateProductAsync(category.Id);

        var response = await Admin().DeleteAsync($"/api/category/{category.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Anonymous().GetAsync($"/api/category/{category.Id}")).StatusCode);
    }

    [Fact]
    public async Task Category_DeleteEmpty_ReturnsNoContent()
    {
        var category = await CreateCategoryAsync();

        var response = await Admin().DeleteAsync($"/api/category/{category.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task BulkUpdate_InvalidItem_ReturnsBadRequest()
    {
        var category = await CreateCategoryAsync();
        var created = await CreateProductAsync(category.Id);

        var response = await Admin().PutAsJsonAsync(
            "/api/product/bulk-update",
            new[] { new { id = created.Id, price = -5 } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var unchanged = await Anonymous().GetFromJsonAsync<ProductResponseDto>($"/api/product/{created.Id}");

        Assert.Equal(created.Price, unchanged!.Price);
    }

    [Fact]
    public async Task BulkUpdate_EmptyList_ReturnsBadRequest()
    {
        var response = await Admin().PutAsJsonAsync("/api/product/bulk-update", Array.Empty<object>());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Inventory_NegativeQuantity_ReturnsBadRequest()
    {
        var response = await Admin().PostAsJsonAsync(
            "/api/inventory/1",
            new ProductService.Application.DTOs.Inventory.InventoryUpdateDto { Quantity = -1, ReorderLevel = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Inventory_CreatedTwice_ReturnsBadRequest()
    {
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(category.Id);
        var body = new ProductService.Application.DTOs.Inventory.InventoryUpdateDto { Quantity = 5, ReorderLevel = 1 };

        Assert.Equal(HttpStatusCode.Created, (await Admin().PostAsJsonAsync($"/api/inventory/{product.Id}", body)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Admin().PostAsJsonAsync($"/api/inventory/{product.Id}", body)).StatusCode);
    }

    [Fact]
    public async Task Category_Unknown_ReturnsNotFound()
    {
        var response = await Anonymous().GetAsync("/api/category/987654");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Inventory_Write_RequiresAdmin()
    {
        var body = new ProductService.Application.DTOs.Inventory.InventoryUpdateDto { Quantity = 5, ReorderLevel = 1 };

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await Anonymous().PostAsJsonAsync("/api/inventory/1", body)).StatusCode);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await Customer().PostAsJsonAsync("/api/inventory/1", body)).StatusCode);
    }

    [Fact]
    public async Task InternalReserve_WithoutKey_IsRejected()
    {
        var response = await Anonymous().PostAsJsonAsync("/internal/inventory/reserve", ValidReservation());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InternalReserve_WithWrongKey_IsRejected()
    {
        var client = Anonymous();
        client.DefaultRequestHeaders.Add("X-Internal-Key", "wrong-key");

        var response = await client.PostAsJsonAsync("/internal/inventory/reserve", ValidReservation());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static ProductService.Application.DTOs.Inventory.StockAdjustmentRequest ValidReservation() => new()
    {
        Items = { new ProductService.Application.DTOs.Inventory.StockLineDto { ProductId = 1, Quantity = 1 } }
    };
}
