using UserService.Domain.Common;
using UserService.Domain.Enums;

namespace UserService.Domain.Entities
{
    public class User : BaseEntity
    {
        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public UserRole Role { get; set; } = UserRole.Customer;

        public bool IsActive { get; set; } = true;
    }
}