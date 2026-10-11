namespace OrderService.Domain.Entities
{
    // A row means the user has read the notification; Dismissed hides it from their list.
    // Per user because admin notifications are shared by all admins.
    public class NotificationRead
    {
        public int NotificationId { get; set; }

        public int UserId { get; set; }

        public bool Dismissed { get; set; }

        public Notification? Notification { get; set; }
    }
}
