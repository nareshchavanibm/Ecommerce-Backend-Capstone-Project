using System.Net;
using System.Net.Http.Json;
using System.Text;
using ProductService.Application.DTOs.Categories;
using ProductService.Application.DTOs.Products;

namespace Services.IntegrationTests;

public class ProductSpecificationApiTests(ProductApiFactory factory) : IClassFixture<ProductApiFactory>
{
    private HttpClient Admin() => factory.CreateClient().AsAdmin();

    private async Task<CategoryResponseDto> CreateCategoryAsync()
    {
        var response = await Admin().PostAsJsonAsync(
            "/api/category",
            new CategoryCreateDto { Name = "Cat-" + Guid.NewGuid().ToString("N"), Description = "Test category" });

        return (await response.Content.ReadFromJsonAsync<CategoryResponseDto>())!;
    }

    private static ProductCreateDto NewProduct(int categoryId, params ProductSpecificationDto[] specs) => new()
    {
        Name = "Phone-" + Guid.NewGuid().ToString("N"),
        Description = "A phone",
        Price = 500m,
        CategoryId = categoryId,
        Specifications = specs.ToList()
    };

    private static ProductSpecificationDto Spec(string? group, string name, string value) =>
        new() { Group = group, Name = name, Value = value };

    [Fact]
    public async Task Create_ThenGetById_ReturnsSpecificationsInOrder()
    {
        var category = await CreateCategoryAsync();

        var response = await Admin().PostAsJsonAsync("/api/product", NewProduct(
            category.Id,
            Spec("Display", "Size", "6.5 inch"),
            Spec(null, "Brand", "Acme")));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = (await response.Content.ReadFromJsonAsync<ProductResponseDto>())!;

        var fetched = (await factory.CreateClient().GetFromJsonAsync<ProductResponseDto>($"/api/product/{created.Id}"))!;

        Assert.Equal(["Size", "Brand"], fetched.Specifications.Select(x => x.Name));
        Assert.Equal(["Display", "General"], fetched.Specifications.Select(x => x.Group));
    }

    [Fact]
    public async Task List_DoesNotCarrySpecifications()
    {
        var category = await CreateCategoryAsync();
        await Admin().PostAsJsonAsync("/api/product", NewProduct(category.Id, Spec(null, "Brand", "Acme")));

        var products = (await factory.CreateClient().GetFromJsonAsync<List<ProductResponseDto>>(
            $"/api/product?category={Uri.EscapeDataString(category.Name)}"))!;

        Assert.All(products, x => Assert.Empty(x.Specifications));
    }

    [Fact]
    public async Task Create_IncompleteSpecification_ReturnsBadRequestWithMessage()
    {
        var category = await CreateCategoryAsync();

        var response = await Admin().PostAsJsonAsync("/api/product", NewProduct(category.Id, Spec(null, "Brand", "")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("needs a value", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Update_ReplacesSpecifications()
    {
        var category = await CreateCategoryAsync();
        var created = (await (await Admin().PostAsJsonAsync("/api/product", NewProduct(category.Id, Spec(null, "Brand", "Acme"))))
            .Content.ReadFromJsonAsync<ProductResponseDto>())!;

        var update = NewProduct(category.Id, Spec("Memory", "RAM", "8 GB"));
        var response = await Admin().PutAsJsonAsync($"/api/product/{created.Id}", update);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var fetched = (await factory.CreateClient().GetFromJsonAsync<ProductResponseDto>($"/api/product/{created.Id}"))!;

        Assert.Equal("RAM", Assert.Single(fetched.Specifications).Name);
    }

    [Fact]
    public async Task Import_ReadsSpecColumnsFromCsv()
    {
        var category = await CreateCategoryAsync();
        var name = "Imported-" + Guid.NewGuid().ToString("N");

        var csv = "Name,Description,Price,CategoryName,Spec:Brand,Spec:Display:Screen Size,Spec:Display:Resolution\n" +
                  $"{name},Nice phone,300,{category.Name},Acme,6.1 inch,\n";

        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "file", "products.csv");

        var response = await Admin().PostAsync("/api/product/import", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var list = (await factory.CreateClient().GetFromJsonAsync<List<ProductResponseDto>>(
            $"/api/product?search={name}"))!;

        var fetched = (await factory.CreateClient().GetFromJsonAsync<ProductResponseDto>($"/api/product/{list[0].Id}"))!;

        Assert.Equal(["Brand", "Screen Size"], fetched.Specifications.Select(x => x.Name));
        Assert.Equal(["General", "Display"], fetched.Specifications.Select(x => x.Group));
    }
}
