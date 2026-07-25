using Application.Features.Courses.ReviewCourse;
using Domain.Common.Interfaces;
using Domain.CourseManagement.Enum;
using Domain.CourseManagement.Events;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace UnitTests.Application.Features.Courses.ReviewCourse
{
    public class CourseReviewedEventHandlerTests
    {
        private readonly Mock<INotificationService> _mockNotificationService;
        private readonly Mock<ILogger<CourseReviewedEventHandler>> _mockLogger;
        private readonly CourseReviewedEventHandler _handler;

        public CourseReviewedEventHandlerTests()
        {
            _mockNotificationService = new Mock<INotificationService>();
            _mockLogger = new Mock<ILogger<CourseReviewedEventHandler>>();
            _handler = new CourseReviewedEventHandler(_mockNotificationService.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task Handle_CoursePublished_ShouldSendApprovalNotification()
        {
            // Arrange
            var courseId = Guid.NewGuid();
            var teacherId = Guid.NewGuid();
            var notification = new CourseReviewedEvent(courseId, "Test Course", CourseStatus.Published, Guid.NewGuid(), teacherId);

            // Act
            await _handler.Handle(notification, CancellationToken.None);

            // Assert
            _mockNotificationService.Verify(s => s.SendAsync(
                teacherId.ToString(),
                "Course approved!",
                It.Is<string>(m => m.Contains("Test Course") && m.Contains("approved")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_CourseRejected_ShouldSendRejectionNotification()
        {
            // Arrange
            var courseId = Guid.NewGuid();
            var teacherId = Guid.NewGuid();
            var notification = new CourseReviewedEvent(courseId, "Test Course", CourseStatus.Rejected, Guid.NewGuid(), teacherId);

            // Act
            await _handler.Handle(notification, CancellationToken.None);

            // Assert
            _mockNotificationService.Verify(s => s.SendAsync(
                teacherId.ToString(),
                "Course rejected",
                It.Is<string>(m => m.Contains("Test Course") && m.Contains("does not meet")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_NonTerminalOutcome_ShouldSkipNotification()
        {
            // Arrange
            var courseId = Guid.NewGuid();
            var teacherId = Guid.NewGuid();
            var notification = new CourseReviewedEvent(courseId, "Test Course", CourseStatus.InReview, Guid.NewGuid(), teacherId);

            // Act
            await _handler.Handle(notification, CancellationToken.None);

            // Assert
            _mockNotificationService.Verify(s => s.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
