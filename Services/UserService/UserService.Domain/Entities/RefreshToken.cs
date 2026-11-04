using UserService.Domain.Common;

namespace UserService.Domain.Entities
{
    // Only a hash of the token is stored, so a database leak does not expose usable tokens.
    public class RefreshToken : BaseEntity
    {
        public int UserId { get; set; }

        public User? User { get; set; }

        public string TokenHash { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }

        public DateTime? RevokedAt { get; set; }

        // Set when the token was rotated; a later reuse of this token signals theft.
        public string? ReplacedByTokenHash { get; set; }
    }
}
