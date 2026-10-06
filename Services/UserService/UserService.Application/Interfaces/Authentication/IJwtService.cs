using UserService.Domain.Entities;

namespace UserService.Application.Interfaces.Authentication
{
    public interface IJwtService
    {
        string GenerateToken(User user);
    }
}