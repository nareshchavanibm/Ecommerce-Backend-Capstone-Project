using UserService.Application.DTOs.Auth;
using UserService.Application.Interfaces.Authentication;
using UserService.Application.Interfaces.Repositories;
using UserService.Application.Interfaces.Services;
using UserService.Domain.Entities;
using UserService.Domain.Enums;

namespace UserService.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IJwtService _jwtService;

        public AuthService(
            IUserRepository userRepository,
            IPasswordHasher passwordHasher,
            IJwtService jwtService)
        {
            _userRepository = userRepository;
            _passwordHasher = passwordHasher;
            _jwtService = jwtService;
        }

        public async Task<LoginResponse> RegisterAsync(
            RegisterRequest request)
        {
            var emailExists =
                await _userRepository.ExistsByEmailAsync(request.Email);

            if (emailExists)
            {
                throw new InvalidOperationException(
                    "A user with this email already exists.");
            }

            var user = new User
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email,
                Role = UserRole.Customer,
                IsActive = true
            };

            user.PasswordHash =
                _passwordHasher.HashPassword(
                    user,
                    request.Password);

            await _userRepository.AddAsync(user);

            var token = _jwtService.GenerateToken(user);

            return new LoginResponse
            {
                Token = token,
                UserId = user.Id,
                Email = user.Email,
                Role = user.Role.ToString()
            };
        }

        public async Task<LoginResponse> LoginAsync(
            LoginRequest request)
        {
            var user =
                await _userRepository.GetByEmailAsync(request.Email);

            if (user == null)
            {
                throw new UnauthorizedAccessException(
                    "Invalid email or password.");
            }

            if (!user.IsActive)
            {
                throw new UnauthorizedAccessException(
                    "User account is inactive.");
            }

            var passwordValid =
                _passwordHasher.VerifyPassword(
                    user,
                    request.Password,
                    user.PasswordHash);

            if (!passwordValid)
            {
                throw new UnauthorizedAccessException(
                    "Invalid email or password.");
            }

            var token = _jwtService.GenerateToken(user);

            return new LoginResponse
            {
                Token = token,
                UserId = user.Id,
                Email = user.Email,
                Role = user.Role.ToString()
            };
        }
    }
}