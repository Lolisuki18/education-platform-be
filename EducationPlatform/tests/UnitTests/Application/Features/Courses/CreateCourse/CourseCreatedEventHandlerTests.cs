using Application.Features.Courses.CreateCourse;
using Domain.Common.Interfaces;
using Domain.CourseManagement.Events;
using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.Enum;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace UnitTests.Application.Features.Courses.CreateCourse
{
    public class CourseCreatedEventHandlerTests
    {
        private readonly Mock<ILogger<CourseCreatedEventHandler>> _mockLogger;
        private readonly Mock<IUnitOfWork> _mockUnitOfWork = new();
        private readonly Mock<IUserRepository> _mockUserRepository = new();
        private readonly Mock<INotificationService> _mockNotifications = new();
        private readonly CourseCreatedEventHandler _handler;

        public CourseCreatedEventHandlerTests()
        {
            _mockLogger = new Mock<ILogger<CourseCreatedEventHandler>>();
            _mockUnitOfWork.Setup(u => u.GetRepository<IUserRepository>()).Returns(_mockUserRepository.Object);
            _mockUserRepository
                .Setup(r => r.GetUserIdsByRoleAsync(Role.Admin, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Guid>());

            _handler = new CourseCreatedEventHandler(_mockUnitOfWork.Object, _mockNotifications.Object, _mockLogger.Object);
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

        [Fact]
        public async Task Handle_ShouldTellEveryAdminThereIsACourseToReview()
        {
            var admin1 = Guid.NewGuid();
            var admin2 = Guid.NewGuid();
            _mockUserRepository
                .Setup(r => r.GetUserIdsByRoleAsync(Role.Admin, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Guid> { admin1, admin2 });

            await _handler.Handle(new CourseCreatedEvent(Guid.NewGuid(), "Algebra"), CancellationToken.None);

            _mockNotifications.Verify(n => n.SendAsync(admin1.ToString(), It.IsAny<string>(),
                It.Is<string>(m => m.Contains("Algebra")), It.IsAny<CancellationToken>()), Times.Once);
            _mockNotifications.Verify(n => n.SendAsync(admin2.ToString(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_WithoutAdmins_ShouldNotFail()
        {
            await _handler.Handle(new CourseCreatedEvent(Guid.NewGuid(), "Algebra"), CancellationToken.None);

            _mockNotifications.Verify(n => n.SendAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
