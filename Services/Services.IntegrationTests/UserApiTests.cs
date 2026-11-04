using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using UserService.Application.DTOs.Auth;

namespace Services.IntegrationTests;

public class UserApiTests(UserApiFactory factory) : IClassFixture<UserApiFactory>
{
    private static RegisterRequest NewRegistration(string? email = null) => new()
    {
        FirstName = "Jane",
        LastName = "Doe",
        Email = email ?? $"jane-{Guid.NewGuid():N}@example.com",
        Password = "Str0ngPassw0rd"
    };

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task Register_ReturnsTokenForNewCustomer()
    {
        var client = factory.CreateClient();
        var request = NewRegistration();

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();

        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
        Assert.Equal(request.Email, body.Email);
        Assert.Equal("Customer", body.Role);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsBadRequest()
    {
        var client = factory.CreateClient();
        var request = NewRegistration();

        await client.PostAsJsonAsync("/api/auth/register", request);

        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("already exists", (await ReadJsonAsync(response)).GetProperty("message").GetString());
    }

    [Theory]
    [InlineData("", "Doe", "jane@example.com", "Str0ngPassw0rd")]
    [InlineData("Jane", "Doe", "not-an-email", "Str0ngPassw0rd")]
    [InlineData("Jane", "Doe", "jane@example.com", "short")]
    public async Task Register_InvalidBody_ReturnsBadRequest(
        string firstName,
        string lastName,
        string email,
        string password)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Password = password
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidBody_ReturnsFirstProblemAsMessageAndFieldErrors()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane@example.com",
            Password = "short"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await ReadJsonAsync(response);

        Assert.Equal("Password must be at least 8 characters.", body.GetProperty("message").GetString());
        Assert.True(body.GetProperty("errors").TryGetProperty("Password", out _));
    }

    [Fact]
    public async Task Register_OversizedPassword_ReturnsBadRequest()
    {
        var request = NewRegistration();
        request.Password = new string('p', 5000);

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_AfterRegistration_ReturnsToken()
    {
        var client = factory.CreateClient();
        var request = NewRegistration();

        await client.PostAsJsonAsync("/api/auth/register", request);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = request.Email, Password = request.Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();

        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();
        var request = NewRegistration();

        await client.PostAsJsonAsync("/api/auth/register", request);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = request.Email, Password = "not-the-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_ReturnsUnauthorized()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = "nobody@example.com", Password = "whatever1" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_MissingPassword_ReturnsBadRequest()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = "jane@example.com", Password = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithoutToken_ReturnsUnauthorized()
    {
        var response = await factory.CreateClient().GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithExpiredToken_ReturnsUnauthorized()
    {
        var client = factory.CreateClient().As(TestTokens.Expired(5));

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithTokenFromLogin_ReturnsIdentity()
    {
        var client = factory.CreateClient();
        var request = NewRegistration();

        var registered = await (await client.PostAsJsonAsync("/api/auth/register", request))
            .Content.ReadFromJsonAsync<LoginResponse>();

        var response = await client.As(registered!.Token).GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadJsonAsync(response);

        Assert.Equal(request.Email, body.GetProperty("email").GetString());
        Assert.Equal("Customer", body.GetProperty("role").GetString());
        Assert.Equal(registered.UserId.ToString(), body.GetProperty("userId").GetString());
    }

    [Fact]
    public async Task AdminEndpoint_AsCustomer_ReturnsForbidden()
    {
        var client = factory.CreateClient();

        var registered = await (await client.PostAsJsonAsync("/api/auth/register", NewRegistration()))
            .Content.ReadFromJsonAsync<LoginResponse>();

        var response = await client.As(registered!.Token).GetAsync("/api/auth/admin");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        var response = await factory.CreateClient().GetAsync("/api/auth/admin");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminEndpoint_AsSeededAdmin_ReturnsOk()
    {
        var client = factory.CreateClient();

        // The startup seeder creates this administrator.
        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = "admin@e.com", Password = "Admin123" });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var body = await login.Content.ReadFromJsonAsync<LoginResponse>();

        Assert.Equal("Admin", body!.Role);

        var response = await client.As(body.Token).GetAsync("/api/auth/admin");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Profile_WithTokenFromRegistration_ReturnsOk()
    {
        var client = factory.CreateClient();

        var registered = await (await client.PostAsJsonAsync("/api/auth/register", NewRegistration()))
            .Content.ReadFromJsonAsync<LoginResponse>();

        var response = await client.As(registered!.Token).GetAsync("/api/auth/profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Profile_WithoutToken_ReturnsUnauthorized()
    {
        var response = await factory.CreateClient().GetAsync("/api/auth/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
