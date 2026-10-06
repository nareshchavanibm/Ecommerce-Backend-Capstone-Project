using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UserService.Domain.Entities;
using UserService.Domain.Enums;

namespace UserService.Infrastructure.Data
{
    public static class AdminSeeder
    {
        public static async Task SeedAsync(AppDbContext context)
        {
            // Check whether an admin already exists
            var adminExists = await context.Users
                .AnyAsync(x => x.Role == UserRole.Admin);

            if (adminExists)
            {
                return;
            }

            var admin = new User
            {
                FirstName = "Admin",
                LastName = "User",
                Email = "admin@e.com",
                Role = UserRole.Admin,
                IsActive = true
            };

            var passwordHasher = new PasswordHasher<User>();

            admin.PasswordHash = passwordHasher.HashPassword(
                admin,
                "Admin123");

            await context.Users.AddAsync(admin);
            await context.SaveChangesAsync();
        }
    }
}