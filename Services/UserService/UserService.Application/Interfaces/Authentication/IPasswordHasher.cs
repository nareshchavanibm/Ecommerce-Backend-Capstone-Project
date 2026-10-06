using UserService.Domain.Entities;

namespace UserService.Application.Interfaces.Authentication
{
    public interface IPasswordHasher
    {
        string HashPassword(User user, string password);

        bool VerifyPassword(
            User user,
            string password,
            string passwordHash);
    }
}