using ECommerce.Application.DTOs.Users;
using ECommerce.Application.Interfaces;
using ECommerce.Application.Interfaces.Repositories;
using ECommerce.Domain.Entities;

namespace ECommerce.Application.Services;

public class AuthService(
    IUserRepository userRepository,
    JwtService jwtService) : IAuthService
{
    public async Task<UserDto> RegisterAsync(
        RegisterUserDto dto)
    {
        var existingUser =
            await userRepository.GetByEmailAsync(dto.Email);

        if (existingUser != null)
        {
            throw new InvalidOperationException(
                "A user with this email already exists.");
        }

        var passwordHash =
            BCrypt.Net.BCrypt.HashPassword(dto.Password);

        var user = new User
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Email = dto.Email,
            PasswordHash = passwordHash,
            Role = "Customer",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var createdUser =
            await userRepository.AddAsync(user);

        return new UserDto
        {
            Id = createdUser.Id,
            FirstName = createdUser.FirstName,
            LastName = createdUser.LastName,
            Email = createdUser.Email,
            Role = createdUser.Role,
            CreatedAt = createdUser.CreatedAt
        };
    }

    public async Task<LoginResponseDto?> LoginAsync(
        LoginDto dto)
    {
        var user =
            await userRepository.GetByEmailAsync(dto.Email);

        if (user == null || !user.IsActive)
        {
            return null;
        }

        var passwordValid =
            BCrypt.Net.BCrypt.Verify(
                dto.Password,
                user.PasswordHash);

        if (!passwordValid)
        {
            return null;
        }

        var token =
            jwtService.GenerateToken(user);

        return new LoginResponseDto
        {
            UserId = user.Id,
            Email = user.Email,
            Role = user.Role,
            Token = token
        };
    }
}