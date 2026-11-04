using System.Net;
using System.Net.Http.Json;
using UserService.Application.DTOs.Auth;

namespace Services.IntegrationTests;

public class RefreshTokenApiTests(UserApiFactory factory) : IClassFixture<UserApiFactory>
{
    private async Task<LoginResponse> RegisterAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FirstName = "Jane",
            LastName = "Doe",
            Email = $"jane-{Guid.NewGuid():N}@example.com",
            Password = "Str0ngPassw0rd"
        });

        return (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
    }

    private static Task<HttpResponseMessage> RefreshAsync(HttpClient client, string token) =>
        client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest { RefreshToken = token });

    [Fact]
    public async Task Register_And_login_return_a_refresh_token()
    {
        var client = factory.CreateClient();
        var registered = await RegisterAsync(client);

        Assert.False(string.IsNullOrWhiteSpace(registered.RefreshToken));

        var login = await (await client.PostAsJsonAsync(
                "/api/auth/login",
                new LoginRequest { Email = registered.Email, Password = "Str0ngPassw0rd" }))
            .Content.ReadFromJsonAsync<LoginResponse>();

        Assert.False(string.IsNullOrWhiteSpace(login!.RefreshToken));
        Assert.NotEqual(registered.RefreshToken, login.RefreshToken);
    }

    [Fact]
    public async Task Refresh_needs_no_access_token_and_returns_a_working_one()
    {
        var client = factory.CreateClient();
        var registered = await RegisterAsync(client);

        var response = await RefreshAsync(client, registered.RefreshToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var renewed = await response.Content.ReadFromJsonAsync<LoginResponse>();

        Assert.NotEqual(registered.RefreshToken, renewed!.RefreshToken);
        Assert.Equal(registered.UserId, renewed.UserId);

        var me = await factory.CreateClient().As(renewed.Token).GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task A_refresh_token_works_only_once()
    {
        var client = factory.CreateClient();
        var registered = await RegisterAsync(client);

        await RefreshAsync(client, registered.RefreshToken);

        var again = await RefreshAsync(client, registered.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, again.StatusCode);
    }

    [Fact]
    public async Task Replaying_a_used_token_also_signs_out_the_newer_one()
    {
        var client = factory.CreateClient();
        var registered = await RegisterAsync(client);

        var renewed = await (await RefreshAsync(client, registered.RefreshToken))
            .Content.ReadFromJsonAsync<LoginResponse>();

        await RefreshAsync(client, registered.RefreshToken);

        var newer = await RefreshAsync(client, renewed!.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, newer.StatusCode);
    }

    [Fact]
    public async Task Unknown_token_returns_unauthorized()
    {
        var response = await RefreshAsync(factory.CreateClient(), "made-up-token");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Empty_token_returns_bad_request()
    {
        var response = await RefreshAsync(factory.CreateClient(), "");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var client = factory.CreateClient();
        var registered = await RegisterAsync(client);

        var logout = await client.PostAsJsonAsync(
            "/api/auth/logout",
            new RefreshRequest { RefreshToken = registered.RefreshToken });

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var refresh = await RefreshAsync(client, registered.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Logout_with_an_unknown_token_still_succeeds()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/logout",
            new RefreshRequest { RefreshToken = "made-up-token" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Another_users_session_survives_a_logout()
    {
        var client = factory.CreateClient();
        var first = await RegisterAsync(client);
        var second = await RegisterAsync(client);

        await client.PostAsJsonAsync("/api/auth/logout", new RefreshRequest { RefreshToken = first.RefreshToken });

        var response = await RefreshAsync(client, second.RefreshToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
