using Application.Exceptions;
using Application.Interface;
using Domain.AuditManagement.Aggregate;
using Domain.Common.Interfaces;
using Domain.CourseManagement.Aggregate;
using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.Enum;
using Domain.NotificationManagement.Aggregate;
using Domain.OrderManagement.Aggregate;

namespace Application.Features.Users.Commands
{
    /// <summary>
    /// The shared steps of erasing an account, used both when people delete their own account and when an
    /// administrator handles an erasure request.
    /// </summary>
    internal static class AccountEraser
    {
        public static async Task EraseAsync(
            IUnitOfWork unitOfWork,
            IUserActivityCache activityCache,
            TimeProvider timeProvider,
            User user,
            string performedBy,
            CancellationToken cancellationToken)
        {
            if (user.IsDeleted)
                throw new BadRequestException("This account is already deleted.");

            var now = timeProvider.GetUtcNow().UtcDateTime;

            if (await unitOfWork.GetRepository<IOrderRepository>().HasOpenOrderAsync(user.UserID, now))
                throw new ConflictException("A payment is still in progress. Finish or wait for it to expire, then try again.");

            if (user.Role == Role.Teacher
                && await unitOfWork.GetRepository<ICourseRepository>().HasPublishedCourseAsync(user.UserID, cancellationToken))
                throw new ConflictException("This teacher still has published courses. They must be unpublished by an administrator first.");

            if (user.Role == Role.Admin && user.IsActive)
            {
                var admins = await unitOfWork.GetRepository<IUserRepository>()
                    .GetUserIdsByRoleAsync(Role.Admin, cancellationToken);

                if (admins.Count <= 1)
                    throw new ConflictException("The last administrator cannot be deleted.");
            }

            user.Erase(now);

            await unitOfWork.CommitAsync(performedBy);

            // Only after the commit, as for any status change: an earlier invalidation lets a concurrent request re-cache the old status
            activityCache.Invalidate(user.UserID);

            // Older audit rows may still carry the e-mail address and phone number; the row written by this
            // very commit (it holds ids only) is kept as the record of the erasure
            await unitOfWork.GetRepository<IAuditRepository>().DeleteUserEntriesAsync(user.UserID, now, cancellationToken);

            // Notifications are personal and have no value without the person; cleaning up late is harmless
            // because the retention job removes them as well
            await unitOfWork.GetRepository<INotificationRepository>().DeleteAllForUserAsync(user.UserID, cancellationToken);
        }
    }
}
