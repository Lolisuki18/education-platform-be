using Domain.Common.Interfaces;
using Domain.CourseManagement.Events;
using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.Enum;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Courses.CreateCourse
{
    public class CourseCreatedEventHandler : INotificationHandler<CourseCreatedEvent>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notificationService;
        private readonly ILogger<CourseCreatedEventHandler> _logger;

        public CourseCreatedEventHandler(
            IUnitOfWork unitOfWork,
            INotificationService notificationService,
            ILogger<CourseCreatedEventHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _notificationService = notificationService;
            _logger = logger;
        }

        public async Task Handle(CourseCreatedEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Domain Event triggered: Course '{Title}' (ID: {CourseId}) was created successfully.",
                notification.Title, notification.CourseId);

            // New courses start in review, so somebody has to be told there is work waiting
            var adminIds = await _unitOfWork.GetRepository<IUserRepository>()
                .GetUserIdsByRoleAsync(Role.Admin, cancellationToken);

            foreach (var adminId in adminIds)
            {
                await _notificationService.SendAsync(
                    adminId.ToString(),
                    "New course waiting for review",
                    $"The course \"{notification.Title}\" was submitted and needs to be reviewed.",
                    cancellationToken);
            }
        }
    }
}
