using Domain.Common.Interfaces;
using Domain.CourseManagement.Enum;
using Domain.CourseManagement.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Features.Courses.ReviewCourse
{
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

            var (title, message) = notification.Outcome switch
            {
                CourseStatus.Published => (
                    "Course approved!",
                    $"Congratulations! Your course \"{notification.CourseTitle}\" has been approved by the Admin. Students can now enroll."),

                CourseStatus.Rejected => (
                    "Course rejected",
                    $"Sorry, your course \"{notification.CourseTitle}\" does not meet the requirements. Please check the Admin's notes and revise it."),

                _ => (string.Empty, string.Empty)
            };

            if (string.IsNullOrEmpty(title))
            {
                _logger.LogWarning(
                    "CourseReviewedEvent for Course {CourseId} has non-terminal status {Status}. Notification skipped.",
                    notification.CourseID, notification.Outcome);
                return;
            }
            await _notificationService.SendAsync(
                userId: notification.TeacherID.ToString(),
                title: title,
                message: message,
                cancellationToken: cancellationToken);
        }
    }
}
