using UserService.Domain.Entities;

namespace UserService.Application.Interfaces.Repositories
{
    public interface IRefreshTokenRepository
    {
        Task<RefreshToken?> GetByHashAsync(string tokenHash);

        Task AddAsync(RefreshToken token);

        Task SaveAsync();

        Task RevokeAllForUserAsync(int userId, DateTime revokedAt);
    }
}
