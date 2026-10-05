using Domain.NotificationManagement.Aggregate;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Implementation
{
    public class NotificationRepository : GenericRepository<Notification>, INotificationRepository
    {
        public NotificationRepository(EducationPlatformDBContext context) : base(context) { }

        public async Task<(List<Notification> Items, int Total)> GetForUserAsync(
            Guid userId,
            bool unreadOnly,
            int pageIndex,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var query = context.Notifications
                .AsNoTracking()
                .Where(n => n.UserID == userId);

            if (unreadOnly)
                query = query.Where(n => n.ReadAt == null);

            var total = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(n => n.CreatedAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (items, total);
        }

        public async Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await context.Notifications
                .AsNoTracking()
                .CountAsync(n => n.UserID == userId && n.ReadAt == null, cancellationToken);
        }

        public async Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;

            return await context.Notifications
                .Where(n => n.UserID == userId && n.ReadAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.ReadAt, now), cancellationToken);
        }
    }
}
