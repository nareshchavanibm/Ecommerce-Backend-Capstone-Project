using OrderService.Domain.Common;

namespace OrderService.Domain.Entities
{
    public class Notification : BaseEntity
    {
        // Null means the notification is for every admin.
        public int? UserId { get; set; }

        public int OrderId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string Link { get; set; } = string.Empty;
    }
}
