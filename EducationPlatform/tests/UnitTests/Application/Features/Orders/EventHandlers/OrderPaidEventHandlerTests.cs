using Application.Features.Orders.EventHandlers;
using Domain.Common.Interfaces;
using Domain.EnrollmentManagement.Aggregate;
using Application.Interface;
using Domain.OrderManagement.Events;
using FluentAssertions;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.Configuration;

namespace UnitTests.Application.Features.Orders.EventHandlers
{
    public class OrderPaidEventHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IEnrollmentRepository> _mockEnrollmentRepository;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly OrderPaidEventHandler _handler;

        public OrderPaidEventHandlerTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockEnrollmentRepository = new Mock<IEnrollmentRepository>();
            _mockEmailService = new Mock<IEmailService>();
            _mockConfig = new Mock<IConfiguration>();

            _mockUnitOfWork
                .Setup(u => u.GetRepository<IEnrollmentRepository>())
                .Returns(_mockEnrollmentRepository.Object);

            _handler = new OrderPaidEventHandler(_mockUnitOfWork.Object, _mockEmailService.Object, _mockConfig.Object);
        }

        [Fact]
        public async Task Handle_ValidEvent_ShouldCreateAndAddEnrollment()
        {
            // Arrange
            var orderId = Guid.NewGuid();
            var studentId = Guid.NewGuid();
            var courseId = Guid.NewGuid();
            var notification = new OrderPaidEvent(orderId, studentId, courseId, DateTime.UtcNow);

            // Act
            await _handler.Handle(notification, CancellationToken.None);

            // Assert
            _mockEnrollmentRepository.Verify(r => r.Add(It.Is<Enrollment>(e =>
                e.StudentID == studentId &&
                e.CourseID == courseId
            )), Times.Once);
        }
    }
}
