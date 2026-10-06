using Microsoft.AspNetCore.Identity;
using UserService.Application.Interfaces.Authentication;
using UserService.Domain.Entities;

namespace UserService.Infrastructure.Authentication
{
    public class PasswordHasher : IPasswordHasher
    {
        private readonly PasswordHasher<User> _passwordHasher = new();

        public string HashPassword(User user, string password)
        {
            return _passwordHasher.HashPassword(user, password);
        }

        public bool VerifyPassword(
            User user,
            string password,
            string passwordHash)
        {
            var result = _passwordHasher.VerifyHashedPassword(
                user,
                passwordHash,
                password);

            return result == PasswordVerificationResult.Success ||
                   result == PasswordVerificationResult.SuccessRehashNeeded;
        }
    }
}