using Microsoft.EntityFrameworkCore;

namespace NotificationService
{
    public class NotificationDbContext : DbContext
    {
        public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options) { }
        public DbSet<Notification> Notifications { get; set; }
    }
} 