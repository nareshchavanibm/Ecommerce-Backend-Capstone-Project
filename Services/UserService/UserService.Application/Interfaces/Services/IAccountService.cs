using UserService.Application.DTOs.Account;

namespace UserService.Application.Interfaces.Services
{
    public interface IAccountService
    {
        Task<ProfileResponse?> GetProfileAsync(int userId);

        Task<ProfileResponse?> UpdateProfileAsync(int userId, UpdateProfileRequest request);

        Task<List<AddressResponse>> GetAddressesAsync(int userId);

        Task<AddressResponse> AddAddressAsync(int userId, AddressRequest request);

        Task<AddressResponse?> UpdateAddressAsync(int userId, int id, AddressRequest request);

        Task<bool> DeleteAddressAsync(int userId, int id);

        Task<AddressResponse?> SetDefaultAddressAsync(int userId, int id);
    }
}
