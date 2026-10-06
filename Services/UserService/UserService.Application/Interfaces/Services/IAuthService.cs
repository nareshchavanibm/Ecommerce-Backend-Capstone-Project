using UserService.Application.DTOs.Auth;

namespace UserService.Application.Interfaces.Services
{
    public interface IAuthService
    {
        Task<LoginResponse> RegisterAsync(RegisterRequest request);

        Task<LoginResponse> LoginAsync(LoginRequest request);
    }
}