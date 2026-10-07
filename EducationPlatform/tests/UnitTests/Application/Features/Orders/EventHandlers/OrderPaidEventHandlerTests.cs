using Application.Features.Orders.EventHandlers;
using Domain.Common.Interfaces;
using Domain.CourseManagement.Aggregate;
using Domain.EnrollmentManagement.Aggregate;
using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.Enum;
using Application.Interface;
using Domain.OrderManagement.Aggregate;
using Domain.OrderManagement.Events;
using Domain.OrderManagement.ValueObject;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Application.Options;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace UnitTests.Application.Features.Orders.EventHandlers
{
    public class OrderPaidEventHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IEnrollmentRepository> _mockEnrollmentRepository;
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<ICourseRepository> _mockCourseRepository;
        private readonly Mock<IOrderRepository> _mockOrderRepository;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly Mock<IAfterCommitQueue> _mockAfterCommit;
        private readonly Mock<ILogger<OrderPaidEventHandler>> _mockLogger;
        private readonly List<Func<Task>> _afterCommitActions = new();
        private readonly OrderPaidEventHandler _handler;

        public OrderPaidEventHandlerTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockEnrollmentRepository = new Mock<IEnrollmentRepository>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockCourseRepository = new Mock<ICourseRepository>();
            _mockOrderRepository = new Mock<IOrderRepository>();
            _mockEmailService = new Mock<IEmailService>();
            _mockAfterCommit = new Mock<IAfterCommitQueue>();
            _mockLogger = new Mock<ILogger<OrderPaidEventHandler>>();

            _mockAfterCommit
                .Setup(q => q.Enqueue(It.IsAny<Func<Task>>()))
                .Callback<Func<Task>>(action => _afterCommitActions.Add(action));

            _mockUnitOfWork.Setup(u => u.GetRepository<IEnrollmentRepository>()).Returns(_mockEnrollmentRepository.Object);
            _mockUnitOfWork.Setup(u => u.GetRepository<IUserRepository>()).Returns(_mockUserRepository.Object);
            _mockUnitOfWork.Setup(u => u.GetRepository<ICourseRepository>()).Returns(_mockCourseRepository.Object);
            _mockUnitOfWork.Setup(u => u.GetRepository<IOrderRepository>()).Returns(_mockOrderRepository.Object);

            _mockEnrollmentRepository
                .Setup(r => r.IsStudentEnrolled(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            _handler = new OrderPaidEventHandler(
                _mockUnitOfWork.Object,
                _mockEmailService.Object,
                Options.Create(new PayOSOptions { FrontendUrl = "http://localhost:3000" }),
                _mockLogger.Object,
                _mockAfterCommit.Object);
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

        [Fact]
        public async Task Handle_AlreadyEnrolled_ShouldNotEnrollOrSendAnything()
        {
            var notification = new OrderPaidEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
            _mockEnrollmentRepository
                .Setup(r => r.IsStudentEnrolled(notification.StudentID, notification.CourseID, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            await _handler.Handle(notification, CancellationToken.None);

            _mockEnrollmentRepository.Verify(r => r.Add(It.IsAny<Enrollment>()), Times.Never);
            _afterCommitActions.Should().BeEmpty();
        }

        [Fact]
        public async Task Handle_ShouldOnlyHandTheEmailOverAfterTheCommit()
        {
            var student = new User(Guid.NewGuid(), "student@example.com", "password123", "0123456789", "Student", null, Role.Student, DateTime.UtcNow, true);
            var course = new Course(Guid.NewGuid(), "Algebra", "Description", 100m, "thumb.png", "algebra", "pre", "outcomes",
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
            var order = new Order(Guid.NewGuid(), Commission.Create(0.15m, 100m), student.UserID, course.CourseID, null);

            _mockUserRepository.Setup(r => r.GetByIdAsync(student.UserID, It.IsAny<CancellationToken>())).ReturnsAsync(student);
            _mockCourseRepository.Setup(r => r.GetByIdAsync(course.CourseID, It.IsAny<CancellationToken>())).ReturnsAsync(course);
            _mockOrderRepository.Setup(r => r.GetByIdAsync(order.OrderID, It.IsAny<CancellationToken>())).ReturnsAsync(order);

            await _handler.Handle(new OrderPaidEvent(order.OrderID, student.UserID, course.CourseID, DateTime.UtcNow), CancellationToken.None);

            // Nothing is sent while the order is still being saved...
            _mockEmailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            _afterCommitActions.Should().ContainSingle();

            // ...only when the unit of work reports a successful commit
            await _afterCommitActions[0]();
            _mockEmailService.Verify(e => e.SendEmailAsync("student@example.com", "Payment Confirmation - Algebra", It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task Handle_EmailShowsWhatWasPaid_AndCannotBeUsedToInjectMarkup()
        {
            var student = new User(Guid.NewGuid(), "student@example.com", "password123", "0123456789", "<b>Mallory</b>", null, Role.Student, DateTime.UtcNow, true);
            var course = new Course(Guid.NewGuid(), "Algebra <a href='https://evil.example'>click</a>", "Description", 100000m, "thumb.png", "algebra", "pre", "outcomes",
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

            // 100,000 list price, 30,000 coupon: the student paid 70,000
            var order = new Order(Guid.NewGuid(), Commission.Create(0.15m, 70000m), student.UserID, course.CourseID, null);

            _mockUserRepository.Setup(r => r.GetByIdAsync(student.UserID, It.IsAny<CancellationToken>())).ReturnsAsync(student);
            _mockCourseRepository.Setup(r => r.GetByIdAsync(course.CourseID, It.IsAny<CancellationToken>())).ReturnsAsync(course);
            _mockOrderRepository.Setup(r => r.GetByIdAsync(order.OrderID, It.IsAny<CancellationToken>())).ReturnsAsync(order);

            string? body = null;
            _mockEmailService
                .Setup(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string, string>((_, _, b) => body = b);

            await _handler.Handle(new OrderPaidEvent(order.OrderID, student.UserID, course.CourseID, DateTime.UtcNow), CancellationToken.None);
            await _afterCommitActions[0]();

            body.Should().NotBeNull();
            body.Should().NotContain("<b>Mallory</b>").And.NotContain("<a href='https://evil.example'>");
            body.Should().Contain("&lt;b&gt;Mallory&lt;/b&gt;");
            body.Should().Contain("70,000 VND").And.NotContain("100,000 VND");
        }
    }
}
