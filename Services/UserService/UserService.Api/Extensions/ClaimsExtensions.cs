using System.Security.Claims;

namespace UserService.Api.Extensions
{
    public static class ClaimsExtensions
    {
        public static string? GetUserId(this ClaimsPrincipal user)
        {
            return user.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        public static string? GetEmail(this ClaimsPrincipal user)
        {
            return user.FindFirstValue(ClaimTypes.Email);
        }

        public static string? GetRole(this ClaimsPrincipal user)
        {
            return user.FindFirstValue(ClaimTypes.Role);
        }
    }
}