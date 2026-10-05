using Domain.Common.Interfaces;

namespace Domain.NotificationManagement.Aggregate
{
    public interface INotificationRepository : IGenericRepository<Notification>
    {
        Task<(List<Notification> Items, int Total)> GetForUserAsync(
            Guid userId,
            bool unreadOnly,
            int pageIndex,
            int pageSize,
            CancellationToken cancellationToken = default);

        Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>Marks every unread notification of the user as read and returns how many changed.</summary>
        Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>Deletes every notification of the user (executed immediately) and returns how many were removed.</summary>
        Task<int> DeleteAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
