using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace Services.IntegrationTests;

// Test settings come from environment variables because each Program.cs reads Jwt values before the host is built.
internal static class TestSettings
{
    public const string JwtKey = "integration-tests-signing-key-0123456789abcdef";

    public const string Issuer = "IntegrationTests";

    public const string Audience = "IntegrationTestClients";

    public const string InternalKey = "integration-internal-key";

    static TestSettings()
    {
        Environment.SetEnvironmentVariable("Jwt__Key", JwtKey);
        Environment.SetEnvironmentVariable("Jwt__Issuer", Issuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", Audience);
        Environment.SetEnvironmentVariable("Internal__ApiKey", InternalKey);
    }

    // Touching this forces the static constructor to run before a host is created.
    public static void Apply()
    {
    }

    // Swaps SQL Server for a private in-memory database.
    public static void UseInMemoryDatabase<TContext>(IServiceCollection services)
        where TContext : DbContext
    {
        services.RemoveAll<DbContextOptions<TContext>>();

        var optionConfigurations = services
            .Where(x => x.ServiceType.IsGenericType &&
                        x.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration") &&
                        x.ServiceType.GenericTypeArguments[0] == typeof(TContext))
            .ToList();

        foreach (var descriptor in optionConfigurations)
        {
            services.Remove(descriptor);
        }

        var name = "tests-" + Guid.NewGuid();

        services.AddDbContext<TContext>(options => options.UseInMemoryDatabase(name));
    }
}

internal static class TestTokens
{
    // Same claim types the services' own JwtService issues.
    public static string Create(int userId, string role = "Customer", string email = "user@test.local", string? name = null)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSettings.JwtKey)),
            SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, role)
        };

        if (name != null)
        {
            claims.Add(new Claim(ClaimTypes.Name, name));
        }

        var token = new JwtSecurityToken(
            issuer: TestSettings.Issuer,
            audience: TestSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string Expired(int userId)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSettings.JwtKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: TestSettings.Issuer,
            audience: TestSettings.Audience,
            claims: [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            notBefore: DateTime.UtcNow.AddHours(-3),
            expires: DateTime.UtcNow.AddHours(-2),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static HttpClient As(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    public static HttpClient AsAdmin(this HttpClient client) =>
        client.As(Create(1, "Admin", "admin@test.local"));

    public static HttpClient AsCustomer(this HttpClient client, int userId = 100) =>
        client.As(Create(userId));
}
