using UserService.Domain.Entities;

namespace UserService.Application.Interfaces.Repositories
{
    public interface IAddressRepository
    {
        Task<List<Address>> GetByUserIdAsync(int userId);

        Task<Address?> GetByIdAsync(int userId, int id);

        Task AddAsync(Address address);

        // Persists tracked changes made to addresses.
        Task SaveAsync();

        Task DeleteAsync(Address address);
    }
}
