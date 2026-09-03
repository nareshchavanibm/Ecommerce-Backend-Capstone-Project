using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ECommerce.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ECommerce.Application.Services;

public class JwtService(IConfiguration configuration)
{
    public string GenerateToken(User user)
    {
        var jwtSettings =
            configuration.GetSection("Jwt");

        var key =
            jwtSettings["Key"]
            ?? throw new InvalidOperationException(
                "JWT Key is not configured.");

        var issuer =
            jwtSettings["Issuer"];

        var audience =
            jwtSettings["Audience"];

        var expiryMinutes =
            int.Parse(
                jwtSettings["ExpiryMinutes"] ?? "60");

        var claims = new List<Claim>
        {
            new(
                ClaimTypes.NameIdentifier,
                user.Id.ToString()),

            new(
                ClaimTypes.Email,
                user.Email),

            new(
                ClaimTypes.Role,
                user.Role)
        };

        var securityKey =
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(key));

        var credentials =
            new SigningCredentials(
                securityKey,
                SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(
                expiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }
}