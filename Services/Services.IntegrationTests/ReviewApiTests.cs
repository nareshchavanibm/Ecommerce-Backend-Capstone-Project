using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using ReviewService.Api.Dtos;

namespace Services.IntegrationTests;

public class ReviewApiTests : IClassFixture<ReviewApiFactory>
{
    private readonly ReviewApiFactory _factory;

    public ReviewApiTests(ReviewApiFactory factory)
    {
        _factory = factory;
        _factory.Purchases.Unavailable = false;
    }

    private HttpClient Anonymous() => _factory.CreateClient();

    // Every test gets its own customer and product so rows from other tests never interfere.
    private static int NewId() => Random.Shared.Next(10_000, 1_000_000);

    private HttpClient Buyer(int userId, int productId, string? name = "Jane Doe")
    {
        _factory.Purchases.Buy(userId, productId);

        return _factory.CreateClient().As(TestTokens.Create(userId, name: name));
    }

    private static ReviewWriteDto Write(int rating = 4, string? body = "Works well.", string? title = "Good") =>
        new() { Rating = rating, Body = body, Title = title };

    private static async Task<string> MessageAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString()!;

    [Fact]
    public async Task Create_WithoutToken_ReturnsUnauthorized()
    {
        var response = await Anonymous().PostAsJsonAsync("/api/review/product/1", Write());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutPurchase_ReturnsForbidden()
    {
        var response = await _factory.CreateClient().AsCustomer(NewId())
            .PostAsJsonAsync($"/api/review/product/{NewId()}", Write());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_AfterPurchase_StoresReviewWithShortName()
    {
        var productId = NewId();
        var client = Buyer(NewId(), productId);

        var response = await client.PostAsJsonAsync($"/api/review/product/{productId}", Write(5, "  Great  "));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var review = (await response.Content.ReadFromJsonAsync<ReviewDto>())!;

        Assert.Equal(5, review.Rating);
        Assert.Equal("Great", review.Body);
        Assert.Equal("Jane D.", review.ReviewerName);
        Assert.True(review.IsMine);
    }

    [Fact]
    public async Task Create_Twice_ReturnsConflict()
    {
        var productId = NewId();
        var client = Buyer(NewId(), productId);

        await client.PostAsJsonAsync($"/api/review/product/{productId}", Write());
        var second = await client.PostAsJsonAsync($"/api/review/product/{productId}", Write(2));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task Create_RatingOutOfRange_ReturnsBadRequest(int rating)
    {
        var productId = NewId();
        var client = Buyer(NewId(), productId);

        var response = await client.PostAsJsonAsync($"/api/review/product/{productId}", Write(rating));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("rating", await MessageAsync(response), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_ThousandWords_IsAccepted()
    {
        var productId = NewId();
        var client = Buyer(NewId(), productId);

        var response = await client.PostAsJsonAsync(
            $"/api/review/product/{productId}",
            Write(body: string.Join(' ', Enumerable.Repeat("word", 1000))));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_MoreThanThousandWords_ReturnsBadRequest()
    {
        var productId = NewId();
        var client = Buyer(NewId(), productId);

        var response = await client.PostAsJsonAsync(
            $"/api/review/product/{productId}",
            Write(body: string.Join(' ', Enumerable.Repeat("word", 1001))));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("1000 words", await MessageAsync(response));
    }

    [Fact]
    public async Task Create_RatingOnly_IsAccepted()
    {
        var productId = NewId();
        var client = Buyer(NewId(), productId);

        var response = await client.PostAsJsonAsync($"/api/review/product/{productId}", Write(3, null, null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_WhenOrderServiceDown_ReturnsServiceUnavailable()
    {
        _factory.Purchases.Unavailable = true;

        var response = await _factory.CreateClient().AsCustomer(NewId())
            .PostAsJsonAsync($"/api/review/product/{NewId()}", Write());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task List_ReturnsSummaryAndDistribution()
    {
        var productId = NewId();

        await Buyer(NewId(), productId).PostAsJsonAsync($"/api/review/product/{productId}", Write(5));
        await Buyer(NewId(), productId).PostAsJsonAsync($"/api/review/product/{productId}", Write(4));
        await Buyer(NewId(), productId).PostAsJsonAsync($"/api/review/product/{productId}", Write(2));

        var page = (await Anonymous().GetFromJsonAsync<ReviewPageDto>($"/api/review/product/{productId}"))!;

        Assert.Equal(3, page.TotalItems);
        Assert.Equal(3, page.Items.Count);
        Assert.Equal(3.7, page.Summary.Average);
        Assert.Equal(new[] { 0, 1, 0, 1, 1 }, page.Summary.Distribution);
        Assert.All(page.Items, x => Assert.False(x.IsMine));
    }

    [Fact]
    public async Task List_SortsByHighestAndPages()
    {
        var productId = NewId();

        foreach (var rating in new[] { 2, 5, 3 })
        {
            await Buyer(NewId(), productId).PostAsJsonAsync($"/api/review/product/{productId}", Write(rating));
        }

        var page = (await Anonymous().GetFromJsonAsync<ReviewPageDto>(
            $"/api/review/product/{productId}?sort=highest&pageSize=2"))!;

        Assert.Equal(new[] { 5, 3 }, page.Items.Select(x => x.Rating));
        Assert.Equal(3, page.TotalItems);
    }

    [Fact]
    public async Task Summaries_ReturnOnlyReviewedProducts()
    {
        var reviewed = NewId();
        var unreviewed = NewId();

        await Buyer(NewId(), reviewed).PostAsJsonAsync($"/api/review/product/{reviewed}", Write(4));

        var summaries = (await Anonymous().GetFromJsonAsync<List<RatingSummaryDto>>(
            $"/api/review/summary?productIds={reviewed},{unreviewed}"))!;

        var only = Assert.Single(summaries);

        Assert.Equal(reviewed, only.ProductId);
        Assert.Equal(4, only.Average);
    }

    [Fact]
    public async Task Mine_ReportsEligibilityAndExistingReview()
    {
        var userId = NewId();
        var productId = NewId();

        var notBought = await _factory.CreateClient().AsCustomer(userId)
            .GetFromJsonAsync<MyReviewStatusDto>($"/api/review/product/{productId}/me");

        Assert.False(notBought!.CanReview);

        var client = Buyer(userId, productId);

        Assert.True((await client.GetFromJsonAsync<MyReviewStatusDto>($"/api/review/product/{productId}/me"))!.CanReview);

        await client.PostAsJsonAsync($"/api/review/product/{productId}", Write());

        var after = (await client.GetFromJsonAsync<MyReviewStatusDto>($"/api/review/product/{productId}/me"))!;

        Assert.NotNull(after.Review);
        Assert.True(after.Review!.IsMine);
    }

    [Fact]
    public async Task Update_OwnReview_ChangesRatingAndMarksEdited()
    {
        var productId = NewId();
        var client = Buyer(NewId(), productId);
        var created = (await (await client.PostAsJsonAsync($"/api/review/product/{productId}", Write(2)))
            .Content.ReadFromJsonAsync<ReviewDto>())!;

        var response = await client.PutAsJsonAsync($"/api/review/{created.Id}", Write(5, "Changed my mind"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = (await response.Content.ReadFromJsonAsync<ReviewDto>())!;

        Assert.Equal(5, updated.Rating);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public async Task Update_AnotherCustomersReview_ReturnsForbidden()
    {
        var productId = NewId();
        var created = (await (await Buyer(NewId(), productId).PostAsJsonAsync($"/api/review/product/{productId}", Write()))
            .Content.ReadFromJsonAsync<ReviewDto>())!;

        var response = await _factory.CreateClient().AsCustomer(NewId())
            .PutAsJsonAsync($"/api/review/{created.Id}", Write(1));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ByOwner_RemovesReview()
    {
        var productId = NewId();
        var client = Buyer(NewId(), productId);
        var created = (await (await client.PostAsJsonAsync($"/api/review/product/{productId}", Write()))
            .Content.ReadFromJsonAsync<ReviewDto>())!;

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/review/{created.Id}")).StatusCode);

        var page = (await Anonymous().GetFromJsonAsync<ReviewPageDto>($"/api/review/product/{productId}"))!;

        Assert.Equal(0, page.TotalItems);
    }

    [Fact]
    public async Task Delete_ByAdmin_RemovesAnyReview()
    {
        var productId = NewId();
        var created = (await (await Buyer(NewId(), productId).PostAsJsonAsync($"/api/review/product/{productId}", Write()))
            .Content.ReadFromJsonAsync<ReviewDto>())!;

        var response = await _factory.CreateClient().AsAdmin().DeleteAsync($"/api/review/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ByOtherCustomer_ReturnsForbidden()
    {
        var productId = NewId();
        var created = (await (await Buyer(NewId(), productId).PostAsJsonAsync($"/api/review/product/{productId}", Write()))
            .Content.ReadFromJsonAsync<ReviewDto>())!;

        var response = await _factory.CreateClient().AsCustomer(NewId()).DeleteAsync($"/api/review/{created.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
