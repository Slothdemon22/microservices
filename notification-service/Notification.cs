using System;

namespace NotificationService
{
    public class Notification
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    }
} 