using System.Security.Cryptography;
using System.Text;
using UserService.Application.DTOs.Auth;
using UserService.Application.Interfaces.Authentication;
using UserService.Application.Interfaces.Repositories;
using UserService.Application.Interfaces.Services;
using UserService.Domain.Entities;
using UserService.Domain.Enums;

namespace UserService.Application.Services
{
    public class AuthSettings
    {
        // How long a session can stay signed in without the refresh token being used.
        public int RefreshTokenDays { get; set; } = 14;
    }

    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IJwtService _jwtService;
        private readonly IRefreshTokenRepository _refreshTokens;
        private readonly AuthSettings _settings;
        private readonly TimeProvider _time;

        private sealed record IssuedToken(RefreshToken Entity, string Plain);

        public AuthService(
            IUserRepository userRepository,
            IPasswordHasher passwordHasher,
            IJwtService jwtService,
            IRefreshTokenRepository refreshTokens,
            AuthSettings settings,
            TimeProvider time)
        {
            _userRepository = userRepository;
            _passwordHasher = passwordHasher;
            _jwtService = jwtService;
            _refreshTokens = refreshTokens;
            _settings = settings;
            _time = time;
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

            return await IssueAsync(user);
        }

        public async Task<LoginResponse> LoginAsync(
            LoginRequest request)
        {
            var user =
                await _userRepository.GetByEmailAsync(request.Email);

            if (user == null)
            {
                // Same hashing work as a real login, so response time does not reveal unknown emails.
                _passwordHasher.HashPassword(new User(), request.Password);

                throw new UnauthorizedAccessException(
                    "Invalid email or password.");
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

            // Checked after the password so account status is only revealed to the owner.
            if (!user.IsActive)
            {
                throw new UnauthorizedAccessException(
                    "User account is inactive.");
            }

            return await IssueAsync(user);
        }

        // Each refresh token works once; using it returns a new pair and retires the old token.
        public async Task<LoginResponse> RefreshAsync(string refreshToken)
        {
            var now = _time.GetUtcNow().UtcDateTime;

            var stored = await _refreshTokens.GetByHashAsync(HashToken(refreshToken))
                ?? throw new UnauthorizedAccessException("Invalid refresh token.");

            if (stored.RevokedAt != null)
            {
                // A retired token coming back means a copy was stolen: end every session for the user.
                await _refreshTokens.RevokeAllForUserAsync(stored.UserId, now);

                throw new UnauthorizedAccessException("Invalid refresh token.");
            }

            if (stored.ExpiresAt <= now)
            {
                throw new UnauthorizedAccessException("The session has expired. Please sign in again.");
            }

            var user = await _userRepository.GetByIdAsync(stored.UserId);

            if (user == null || !user.IsActive)
            {
                throw new UnauthorizedAccessException("Invalid refresh token.");
            }

            var next = NewRefreshToken(user.Id, now);

            stored.RevokedAt = now;
            stored.ReplacedByTokenHash = next.Entity.TokenHash;

            // Saving the new token also saves the retirement of the old one.
            await _refreshTokens.AddAsync(next.Entity);

            return Response(user, next.Plain);
        }

        public async Task LogoutAsync(string refreshToken)
        {
            var stored = await _refreshTokens.GetByHashAsync(HashToken(refreshToken));

            if (stored == null || stored.RevokedAt != null)
            {
                return;
            }

            stored.RevokedAt = _time.GetUtcNow().UtcDateTime;

            await _refreshTokens.SaveAsync();
        }

        private async Task<LoginResponse> IssueAsync(User user)
        {
            var token = NewRefreshToken(user.Id, _time.GetUtcNow().UtcDateTime);

            await _refreshTokens.AddAsync(token.Entity);

            return Response(user, token.Plain);
        }

        private LoginResponse Response(User user, string refreshToken) => new()
        {
            Token = _jwtService.GenerateToken(user),
            RefreshToken = refreshToken,
            UserId = user.Id,
            Email = user.Email,
            Role = user.Role.ToString()
        };

        private IssuedToken NewRefreshToken(int userId, DateTime now)
        {
            var plain = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');

            return new IssuedToken(
                new RefreshToken
                {
                    UserId = userId,
                    TokenHash = HashToken(plain),
                    CreatedAt = now,
                    ExpiresAt = now.AddDays(_settings.RefreshTokenDays)
                },
                plain);
        }

        private static string HashToken(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
