using UserService.Domain.Entities;

namespace UserService.Application.Interfaces.Repositories
{
    public interface IUserRepository
    {
        Task<User?> GetByEmailAsync(string email);

        Task<User?> GetByIdAsync(int id);

        Task AddAsync(User user);

        Task<bool> ExistsByEmailAsync(string email);
    }
}