using Application.Features.Courses.CreateCourse;
using Domain.CourseManagement.Events;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace UnitTests.Application.Features.Courses.CreateCourse
{
    public class CourseCreatedEventHandlerTests
    {
        private readonly Mock<ILogger<CourseCreatedEventHandler>> _mockLogger;
        private readonly CourseCreatedEventHandler _handler;

        public CourseCreatedEventHandlerTests()
        {
            _mockLogger = new Mock<ILogger<CourseCreatedEventHandler>>();
            _handler = new CourseCreatedEventHandler(_mockLogger.Object);
        }

        [Fact]
        public async Task Handle_ValidEvent_ShouldLogInformationAndCompleteSuccessfully()
        {
            // Arrange
            var courseId = Guid.NewGuid();
            var notification = new CourseCreatedEvent(courseId, "Introduction to Testing");

            // Act
            await _handler.Handle(notification, CancellationToken.None);

            // Assert
            _mockLogger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, type) => state.ToString()!.Contains("Introduction to Testing") && state.ToString()!.Contains(courseId.ToString())),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }
    }
}
