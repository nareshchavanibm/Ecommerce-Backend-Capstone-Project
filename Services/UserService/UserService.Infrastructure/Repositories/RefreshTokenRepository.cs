using Microsoft.EntityFrameworkCore;
using UserService.Application.Interfaces.Repositories;
using UserService.Domain.Entities;
using UserService.Infrastructure.Data;

namespace UserService.Infrastructure.Repositories
{
    public class RefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly AppDbContext _context;

        public RefreshTokenRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<RefreshToken?> GetByHashAsync(string tokenHash)
        {
            return _context.RefreshTokens
                .FirstOrDefaultAsync(x => x.TokenHash == tokenHash);
        }

        public async Task AddAsync(RefreshToken token)
        {
            await _context.RefreshTokens.AddAsync(token);
            await _context.SaveChangesAsync();
        }

        public Task SaveAsync()
        {
            return _context.SaveChangesAsync();
        }

        public async Task RevokeAllForUserAsync(int userId, DateTime revokedAt)
        {
            var active = await _context.RefreshTokens
                .Where(x => x.UserId == userId && x.RevokedAt == null)
                .ToListAsync();

            foreach (var token in active)
            {
                token.RevokedAt = revokedAt;
            }

            await _context.SaveChangesAsync();
        }
    }
}
