using Microsoft.EntityFrameworkCore;
using UserService.Application.Interfaces.Authentication;
using UserService.Application.Interfaces.Repositories;
using UserService.Application.Interfaces.Services;
using UserService.Application.Services;
using UserService.Infrastructure.Authentication;
using UserService.Infrastructure.Data;
using UserService.Infrastructure.Repositories;

namespace UserService.Api.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddUserService(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Database
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(
                    configuration.GetConnectionString("DefaultConnection")));

            // Repositories
            services.AddScoped<IUserRepository, UserRepository>();

            // Authentication
            services.AddScoped<IPasswordHasher, PasswordHasher>();
            services.AddScoped<IJwtService, JwtService>();

            // Application services
            services.AddScoped<IAuthService, AuthService>();

            return services;
        }
    }
}