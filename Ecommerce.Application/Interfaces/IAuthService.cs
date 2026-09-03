using ECommerce.Application.DTOs.Users;

namespace ECommerce.Application.Interfaces;

public interface IAuthService
{
    Task<UserDto> RegisterAsync(RegisterUserDto dto);

    Task<LoginResponseDto?> LoginAsync(LoginDto dto);
}