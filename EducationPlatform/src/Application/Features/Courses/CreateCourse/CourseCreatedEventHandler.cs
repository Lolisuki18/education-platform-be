using Domain.CourseManagement.Events;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Courses.CreateCourse
{
    public class CourseCreatedEventHandler : INotificationHandler<CourseCreatedEvent>
    {
        private readonly ILogger<CourseCreatedEventHandler> _logger;

        public CourseCreatedEventHandler(ILogger<CourseCreatedEventHandler> logger)
        {
            _logger = logger;
        }

        public Task Handle(CourseCreatedEvent notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Domain Event triggered: Course '{Title}' (ID: {CourseId}) was created successfully.",
                notification.Title, notification.CourseId);

            return Task.CompletedTask;
        }
    }
}
