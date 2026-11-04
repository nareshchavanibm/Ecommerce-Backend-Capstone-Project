using UserService.Application.DTOs.Auth;
using UserService.Application.Services;
using UserService.Application.Validators;
using FluentValidation.TestHelper;
using AuthSvc = UserService.Application.Services.AuthService;

namespace Services.Tests;

public class RefreshTokenTests
{
    private readonly FakeUserRepository _users = new();

    private readonly FakeRefreshTokenRepository _refreshTokens = new();

    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero));

    private AuthSvc CreateService(int days = 14) =>
        new(_users, new FakePasswordHasher(), new FakeJwtService(), _refreshTokens, new AuthSettings { RefreshTokenDays = days }, _time);

    private static RegisterRequest Registration() => new()
    {
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane@example.com",
        Password = "Str0ngPassw0rd"
    };

    [Fact]
    public async Task Register_And_login_each_issue_a_refresh_token()
    {
        var service = CreateService();

        var registered = await service.RegisterAsync(Registration());
        var login = await service.LoginAsync(new LoginRequest { Email = "jane@example.com", Password = "Str0ngPassw0rd" });

        Assert.False(string.IsNullOrWhiteSpace(registered.RefreshToken));
        Assert.False(string.IsNullOrWhiteSpace(login.RefreshToken));
        Assert.NotEqual(registered.RefreshToken, login.RefreshToken);
        Assert.Equal(2, _refreshTokens.Tokens.Count);
    }

    [Fact]
    public async Task Only_a_hash_of_the_token_is_stored()
    {
        var registered = await CreateService().RegisterAsync(Registration());

        var stored = Assert.Single(_refreshTokens.Tokens);

        Assert.NotEqual(registered.RefreshToken, stored.TokenHash);
        Assert.DoesNotContain(registered.RefreshToken, stored.TokenHash);
        Assert.Equal(64, stored.TokenHash.Length);
    }

    [Fact]
    public async Task The_token_expires_after_the_configured_days()
    {
        await CreateService(days: 7).RegisterAsync(Registration());

        var stored = Assert.Single(_refreshTokens.Tokens);

        Assert.Equal(_time.GetUtcNow().UtcDateTime.AddDays(7), stored.ExpiresAt);
    }

    [Fact]
    public async Task Refresh_returns_new_tokens_and_retires_the_old_one()
    {
        var service = CreateService();
        var first = await service.RegisterAsync(Registration());

        var second = await service.RefreshAsync(first.RefreshToken);

        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        Assert.Equal("token-for-jane@example.com", second.Token);
        Assert.Equal(first.UserId, second.UserId);
        Assert.Equal("Customer", second.Role);

        var old = _refreshTokens.Tokens[0];

        Assert.NotNull(old.RevokedAt);
        Assert.Equal(_refreshTokens.Tokens[1].TokenHash, old.ReplacedByTokenHash);
        Assert.Null(_refreshTokens.Tokens[1].RevokedAt);
    }

    [Fact]
    public async Task A_refresh_token_can_be_chained()
    {
        var service = CreateService();
        var current = await service.RegisterAsync(Registration());

        for (var i = 0; i < 3; i++)
        {
            current = await service.RefreshAsync(current.RefreshToken);
        }

        Assert.Equal(4, _refreshTokens.Tokens.Count);
        Assert.Single(_refreshTokens.Tokens, x => x.RevokedAt == null);
    }

    [Fact]
    public async Task Unknown_token_is_rejected()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => CreateService().RefreshAsync("not-a-real-token"));
    }

    [Fact]
    public async Task Expired_token_is_rejected()
    {
        var service = CreateService(days: 1);
        var registered = await service.RegisterAsync(Registration());

        _time.Advance(TimeSpan.FromDays(2));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.RefreshAsync(registered.RefreshToken));
    }

    [Fact]
    public async Task Reusing_a_retired_token_revokes_every_session_of_that_user()
    {
        var service = CreateService();
        var first = await service.RegisterAsync(Registration());
        var second = await service.RefreshAsync(first.RefreshToken);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.RefreshAsync(first.RefreshToken));

        // The legitimate holder of the newest token is signed out too.
        Assert.All(_refreshTokens.Tokens, x => Assert.NotNull(x.RevokedAt));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.RefreshAsync(second.RefreshToken));
    }

    [Fact]
    public async Task Reuse_by_one_user_does_not_affect_another()
    {
        var service = CreateService();
        var jane = await service.RegisterAsync(Registration());

        var other = Registration();
        other.Email = "john@example.com";
        var john = await service.RegisterAsync(other);

        await service.RefreshAsync(jane.RefreshToken);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.RefreshAsync(jane.RefreshToken));

        Assert.NotNull(await service.RefreshAsync(john.RefreshToken));
    }

    [Fact]
    public async Task Inactive_user_cannot_refresh()
    {
        var service = CreateService();
        var registered = await service.RegisterAsync(Registration());

        _users.Users.Single().IsActive = false;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.RefreshAsync(registered.RefreshToken));
    }

    [Fact]
    public async Task Logout_revokes_the_token()
    {
        var service = CreateService();
        var registered = await service.RegisterAsync(Registration());

        await service.LogoutAsync(registered.RefreshToken);

        Assert.NotNull(Assert.Single(_refreshTokens.Tokens).RevokedAt);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.RefreshAsync(registered.RefreshToken));
    }

    [Fact]
    public async Task Logout_with_an_unknown_token_does_nothing()
    {
        await CreateService().LogoutAsync("unknown");
    }

    [Theory]
    [InlineData("")]
    public void Validator_rejects_a_missing_token(string token)
    {
        new RefreshRequestValidator()
            .TestValidate(new RefreshRequest { RefreshToken = token })
            .ShouldHaveValidationErrorFor(x => x.RefreshToken);
    }

    [Fact]
    public void Validator_rejects_an_oversized_token()
    {
        new RefreshRequestValidator()
            .TestValidate(new RefreshRequest { RefreshToken = new string('a', 201) })
            .ShouldHaveValidationErrorFor(x => x.RefreshToken);
    }
}
