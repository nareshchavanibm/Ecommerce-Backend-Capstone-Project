using FluentValidation.TestHelper;
using UserService.Application.DTOs.Auth;
using UserService.Application.Interfaces.Authentication;
using UserService.Application.Interfaces.Repositories;
using UserService.Application.Services;
using UserService.Application.Validators;
using UserService.Domain.Entities;
using UserService.Domain.Enums;
using AuthSvc = UserService.Application.Services.AuthService;

namespace Services.Tests;

internal sealed class FakeUserRepository : IUserRepository
{
    private int _nextId = 1;

    public List<User> Users { get; } = new();

    public Task<User?> GetByEmailAsync(string email) =>
        Task.FromResult(Users.FirstOrDefault(x => x.Email == email));

    public Task<User?> GetByIdAsync(int id) =>
        Task.FromResult(Users.FirstOrDefault(x => x.Id == id));

    public Task AddAsync(User user)
    {
        user.Id = _nextId++;
        Users.Add(user);

        return Task.CompletedTask;
    }

    public Task<bool> ExistsByEmailAsync(string email) =>
        Task.FromResult(Users.Any(x => x.Email == email));

    public Task SaveAsync() => Task.CompletedTask;
}

internal sealed class FakePasswordHasher : IPasswordHasher
{
    public string HashPassword(User user, string password) => "hashed:" + password;

    public bool VerifyPassword(User user, string password, string passwordHash) =>
        passwordHash == "hashed:" + password;
}

internal sealed class FakeJwtService : IJwtService
{
    public string GenerateToken(User user) => "token-for-" + user.Email;
}

internal sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
{
    public List<RefreshToken> Tokens { get; } = new();

    public Task<RefreshToken?> GetByHashAsync(string tokenHash) =>
        Task.FromResult(Tokens.FirstOrDefault(x => x.TokenHash == tokenHash));

    public Task AddAsync(RefreshToken token)
    {
        Tokens.Add(token);

        return Task.CompletedTask;
    }

    public Task SaveAsync() => Task.CompletedTask;

    public Task RevokeAllForUserAsync(int userId, DateTime revokedAt)
    {
        foreach (var token in Tokens.Where(x => x.UserId == userId && x.RevokedAt == null))
        {
            token.RevokedAt = revokedAt;
        }

        return Task.CompletedTask;
    }
}

internal sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public void Advance(TimeSpan by) => _now += by;

    public override DateTimeOffset GetUtcNow() => _now;
}

public class AuthServiceTests
{
    private readonly FakeUserRepository _users = new();

    private readonly FakeRefreshTokenRepository _refreshTokens = new();

    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero));

    private AuthSvc CreateService() =>
        new(_users, new FakePasswordHasher(), new FakeJwtService(), _refreshTokens, new AuthSettings { RefreshTokenDays = 14 }, _time);

    private static RegisterRequest NewRegistration(string email = "jane@example.com") => new()
    {
        FirstName = "Jane",
        LastName = "Doe",
        Email = email,
        Password = "Str0ngPassw0rd"
    };

    [Fact]
    public async Task Register_CreatesActiveCustomerWithHashedPassword()
    {
        var response = await CreateService().RegisterAsync(NewRegistration());

        var user = Assert.Single(_users.Users);

        Assert.Equal(UserRole.Customer, user.Role);
        Assert.True(user.IsActive);
        Assert.Equal("hashed:Str0ngPassw0rd", user.PasswordHash);
        Assert.NotEqual("Str0ngPassw0rd", user.PasswordHash);

        Assert.Equal("token-for-jane@example.com", response.Token);
        Assert.Equal(user.Id, response.UserId);
        Assert.Equal("Customer", response.Role);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Throws()
    {
        var service = CreateService();

        await service.RegisterAsync(NewRegistration());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RegisterAsync(NewRegistration()));

        Assert.Single(_users.Users);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokenAndRole()
    {
        var service = CreateService();

        await service.RegisterAsync(NewRegistration());

        var response = await service.LoginAsync(new LoginRequest
        {
            Email = "jane@example.com",
            Password = "Str0ngPassw0rd"
        });

        Assert.Equal("token-for-jane@example.com", response.Token);
        Assert.Equal("jane@example.com", response.Email);
        Assert.Equal("Customer", response.Role);
    }

    [Fact]
    public async Task Login_UnknownEmail_ThrowsUnauthorized()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => CreateService().LoginAsync(new LoginRequest
            {
                Email = "nobody@example.com",
                Password = "whatever1"
            }));
    }

    [Fact]
    public async Task Login_WrongPassword_ThrowsUnauthorized()
    {
        var service = CreateService();

        await service.RegisterAsync(NewRegistration());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.LoginAsync(new LoginRequest
            {
                Email = "jane@example.com",
                Password = "wrong-password"
            }));
    }

    [Fact]
    public async Task Login_InactiveUser_ThrowsUnauthorized()
    {
        var service = CreateService();

        await service.RegisterAsync(NewRegistration());

        _users.Users.Single().IsActive = false;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.LoginAsync(new LoginRequest
            {
                Email = "jane@example.com",
                Password = "Str0ngPassw0rd"
            }));
    }

    [Fact]
    public async Task Login_WrongPasswordAndUnknownEmail_GiveSameMessage()
    {
        var service = CreateService();

        await service.RegisterAsync(NewRegistration());

        var wrongPassword = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.LoginAsync(new LoginRequest { Email = "jane@example.com", Password = "nope" }));

        var unknownEmail = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.LoginAsync(new LoginRequest { Email = "x@example.com", Password = "nope" }));

        // Same text, so the API cannot be used to discover which emails are registered.
        Assert.Equal(wrongPassword.Message, unknownEmail.Message);
    }
}

public class AuthValidatorTests
{
    private readonly RegisterRequestValidator _register = new();

    private readonly LoginRequestValidator _login = new();

    private static RegisterRequest ValidRegistration() => new()
    {
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane@example.com",
        Password = "Str0ngPassw0rd"
    };

    [Fact]
    public void Register_ValidRequest_Passes()
    {
        _register.TestValidate(ValidRegistration()).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("", "Doe", "jane@example.com", "Str0ngPassw0rd")]
    [InlineData("Jane", "", "jane@example.com", "Str0ngPassw0rd")]
    [InlineData("Jane", "Doe", "", "Str0ngPassw0rd")]
    [InlineData("Jane", "Doe", "not-an-email", "Str0ngPassw0rd")]
    [InlineData("Jane", "Doe", "jane@example.com", "")]
    [InlineData("Jane", "Doe", "jane@example.com", "short")]
    public void Register_InvalidRequest_Fails(
        string firstName,
        string lastName,
        string email,
        string password)
    {
        var result = _register.TestValidate(new RegisterRequest
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Password = password
        });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Register_NamesLongerThan100_Fail()
    {
        var request = ValidRegistration();
        request.FirstName = new string('a', 101);
        request.LastName = new string('b', 101);

        var result = _register.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.FirstName);
        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Fact]
    public void Register_OversizedEmailOrPassword_Fails()
    {
        var request = ValidRegistration();
        request.Email = new string('a', 250) + "@example.com";
        request.Password = new string('p', 129);

        var result = _register.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Email);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Login_OversizedPassword_Fails()
    {
        var result = _login.TestValidate(new LoginRequest
        {
            Email = "jane@example.com",
            Password = new string('p', 129)
        });

        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Login_ValidRequest_Passes()
    {
        _login.TestValidate(new LoginRequest { Email = "jane@example.com", Password = "anything" })
            .ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("", "password")]
    [InlineData("not-an-email", "password")]
    [InlineData("jane@example.com", "")]
    public void Login_InvalidRequest_Fails(string email, string password)
    {
        var result = _login.TestValidate(new LoginRequest { Email = email, Password = password });

        Assert.False(result.IsValid);
    }
}
