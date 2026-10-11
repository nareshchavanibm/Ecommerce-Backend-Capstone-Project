using UserService.Application.DTOs.Account;
using UserService.Application.Interfaces.Repositories;
using UserService.Application.Interfaces.Services;
using UserService.Domain.Entities;

namespace UserService.Application.Services
{
    public class AccountService : IAccountService
    {
        private const int MaxAddresses = 10;

        private readonly IUserRepository _userRepository;
        private readonly IAddressRepository _addressRepository;

        public AccountService(
            IUserRepository userRepository,
            IAddressRepository addressRepository)
        {
            _userRepository = userRepository;
            _addressRepository = addressRepository;
        }

        public async Task<ProfileResponse?> GetProfileAsync(int userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);

            return user == null ? null : MapProfile(user);
        }

        public async Task<ProfileResponse?> UpdateProfileAsync(
            int userId,
            UpdateProfileRequest request)
        {
            var user = await _userRepository.GetByIdAsync(userId);

            if (user == null)
                return null;

            user.FirstName = request.FirstName.Trim();
            user.LastName = request.LastName.Trim();

            await _userRepository.SaveAsync();

            return MapProfile(user);
        }

        public async Task<List<AddressResponse>> GetAddressesAsync(int userId)
        {
            var addresses = await _addressRepository.GetByUserIdAsync(userId);

            return addresses.Select(MapAddress).ToList();
        }

        public async Task<AddressResponse> AddAddressAsync(
            int userId,
            AddressRequest request)
        {
            var existing = await _addressRepository.GetByUserIdAsync(userId);

            if (existing.Count >= MaxAddresses)
                throw new InvalidOperationException(
                    $"You can save up to {MaxAddresses} addresses.");

            var address = new Address { UserId = userId };

            Apply(address, request);

            // The first address is always the default.
            address.IsDefault = existing.Count == 0 || request.IsDefault;

            if (address.IsDefault)
                ClearDefault(existing);

            await _addressRepository.AddAsync(address);

            return MapAddress(address);
        }

        public async Task<AddressResponse?> UpdateAddressAsync(
            int userId,
            int id,
            AddressRequest request)
        {
            var addresses = await _addressRepository.GetByUserIdAsync(userId);
            var address = addresses.FirstOrDefault(x => x.Id == id);

            if (address == null)
                return null;

            Apply(address, request);

            if (request.IsDefault && !address.IsDefault)
            {
                ClearDefault(addresses);
                address.IsDefault = true;
            }

            await _addressRepository.SaveAsync();

            return MapAddress(address);
        }

        public async Task<bool> DeleteAddressAsync(int userId, int id)
        {
            var addresses = await _addressRepository.GetByUserIdAsync(userId);
            var address = addresses.FirstOrDefault(x => x.Id == id);

            if (address == null)
                return false;

            await _addressRepository.DeleteAsync(address);

            // Keep one default: promote the most recently added address.
            if (address.IsDefault)
            {
                var next = addresses
                    .Where(x => x.Id != id)
                    .OrderByDescending(x => x.CreatedAt)
                    .FirstOrDefault();

                if (next != null)
                {
                    next.IsDefault = true;
                    await _addressRepository.SaveAsync();
                }
            }

            return true;
        }

        public async Task<AddressResponse?> SetDefaultAddressAsync(int userId, int id)
        {
            var addresses = await _addressRepository.GetByUserIdAsync(userId);
            var address = addresses.FirstOrDefault(x => x.Id == id);

            if (address == null)
                return null;

            ClearDefault(addresses);
            address.IsDefault = true;

            await _addressRepository.SaveAsync();

            return MapAddress(address);
        }

        private static void ClearDefault(IEnumerable<Address> addresses)
        {
            foreach (var item in addresses)
                item.IsDefault = false;
        }

        private static void Apply(Address address, AddressRequest request)
        {
            address.FullName = request.FullName.Trim();
            address.Phone = request.Phone.Trim();
            address.AddressLine1 = request.AddressLine1.Trim();
            address.AddressLine2 = string.IsNullOrWhiteSpace(request.AddressLine2)
                ? null
                : request.AddressLine2.Trim();
            address.City = request.City.Trim();
            address.State = request.State.Trim();
            address.PostalCode = request.PostalCode.Trim();
            address.Country = request.Country.Trim();
        }

        private static ProfileResponse MapProfile(User user)
        {
            return new ProfileResponse
            {
                UserId = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Role = user.Role.ToString()
            };
        }

        private static AddressResponse MapAddress(Address address)
        {
            return new AddressResponse
            {
                Id = address.Id,
                FullName = address.FullName,
                Phone = address.Phone,
                AddressLine1 = address.AddressLine1,
                AddressLine2 = address.AddressLine2,
                City = address.City,
                State = address.State,
                PostalCode = address.PostalCode,
                Country = address.Country,
                IsDefault = address.IsDefault
            };
        }
    }
}
