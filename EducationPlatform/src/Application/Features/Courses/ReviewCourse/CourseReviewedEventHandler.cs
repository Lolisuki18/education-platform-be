using Domain.Common.Interfaces;
using Domain.CourseManagement.Enum;
using Domain.CourseManagement.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Courses.ReviewCourse
{
    /// <summary>
    /// Event Handler for CourseReviewedEvent.
    ///
    /// SRP guarantee:
    ///   - ReviewCourseCommandHandler's ONLY job: update domain state + persist.
    ///   - THIS handler's ONLY job: react to the outcome and notify the Teacher.
    ///
    /// Execution model:
    ///   The DomainEventDispatcherInterceptor calls MediatR.Publish() INSIDE
    ///   the same SaveChanges() call, so the notification is dispatched
    ///   synchronously with the transaction but in a separate handler scope.
    ///   If notification fails, it does NOT roll back the domain transaction
    ///   (fire-and-forget side-effect pattern).
    /// </summary>
    public class CourseReviewedEventHandler : INotificationHandler<CourseReviewedEvent>
    {
        private readonly INotificationService _notificationService;
        private readonly ILogger<CourseReviewedEventHandler> _logger;

        public CourseReviewedEventHandler(
            INotificationService notificationService,
            ILogger<CourseReviewedEventHandler> logger)
        {
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task Handle(
            CourseReviewedEvent notification,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Domain Event Handled: Course '{Title}' (ID: {CourseId}) reviewed → Outcome: {Outcome}",
                notification.CourseTitle,
                notification.CourseID,
                notification.Outcome);

            // Compose teacher-facing message based on review outcome
            var (title, message) = notification.Outcome switch
            {
                CourseStatus.Published => (
                    "🎉 Khóa học được duyệt!",
                    $"Chúc mừng! Khóa học \"{notification.CourseTitle}\" của bạn đã được Admin xét duyệt thành công. Học viên bây giờ có thể đăng ký."),

                CourseStatus.Rejected => (
                    "❌ Khóa học chưa đạt yêu cầu",
                    $"Rất tiếc, khóa học \"{notification.CourseTitle}\" của bạn chưa đáp ứng tiêu chuẩn. Vui lòng kiểm tra ghi chú của Admin và chỉnh sửa lại."),

                // Any other transitional status — log only
                _ => (string.Empty, string.Empty)
            };

            // Skip notification for non-terminal states (e.g. InReview)
            if (string.IsNullOrEmpty(title))
            {
                _logger.LogWarning(
                    "CourseReviewedEvent for Course {CourseId} has non-terminal status {Status}. Notification skipped.",
                    notification.CourseID, notification.Outcome);
                return;
            }

            // Notify the Teacher who owns the course — TeacherID is carried
            // directly in the event, so no extra DB fetch is needed here.
            await _notificationService.SendAsync(
                userId: notification.TeacherID.ToString(),
                title: title,
                message: message,
                cancellationToken: cancellationToken);
        }
    }
}
