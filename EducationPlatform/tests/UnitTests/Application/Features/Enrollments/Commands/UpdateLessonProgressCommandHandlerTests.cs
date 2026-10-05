using Application.Exceptions;
using Application.Features.Enrollments.Commands;
using Application.Interface;
using Domain.Common.Interfaces;
using Domain.EnrollmentManagement.Aggregate;
using FluentAssertions;
using MediatR;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace UnitTests.Application.Features.Enrollments.Commands
{
    public class UpdateLessonProgressCommandHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IEnrollmentRepository> _mockEnrollmentRepository;
        private readonly Mock<ICurrentUser> _mockCurrentUser;
        private readonly UpdateLessonProgressCommandHandler _handler;

        public UpdateLessonProgressCommandHandlerTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockEnrollmentRepository = new Mock<IEnrollmentRepository>();
            _mockCurrentUser = new Mock<ICurrentUser>();

            _mockUnitOfWork
                .Setup(u => u.GetRepository<IEnrollmentRepository>())
                .Returns(_mockEnrollmentRepository.Object);

            _handler = new UpdateLessonProgressCommandHandler(
                _mockUnitOfWork.Object,
                _mockCurrentUser.Object);
        }

        [Fact]
        public async Task Handle_UserNotAuthenticated_ShouldThrowAuthenticateException()
        {
            // Arrange
            _mockCurrentUser.Setup(u => u.Id).Returns((Guid?)null);
            var command = new UpdateLessonProgressCommand { EnrollmentID = Guid.NewGuid() };

            // Act
            Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<AuthenticateException>()
                .WithMessage("User must be authenticated.");
        }

        [Fact]
        public async Task Handle_EnrollmentDoesNotExist_ShouldThrowForbiddenException()
        {
            // Arrange
            var studentId = Guid.NewGuid();
            var enrollmentId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(studentId);

            _mockEnrollmentRepository
                .Setup(r => r.GetByIdAsync(enrollmentId))
                .ReturnsAsync((Enrollment?)null);

            var command = new UpdateLessonProgressCommand { EnrollmentID = enrollmentId };

            // Act
            Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<ForbiddenException>()
                .WithMessage("Not authorized to update this enrollment.");
        }

        [Fact]
        public async Task Handle_UserNotEnrollmentOwner_ShouldThrowForbiddenException()
        {
            // Arrange
            var studentId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var enrollmentId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(studentId);

            var enrollment = new Enrollment(enrollmentId, ownerId, Guid.NewGuid(), DateTime.UtcNow);

            _mockEnrollmentRepository
                .Setup(r => r.GetByIdAsync(enrollmentId))
                .ReturnsAsync(enrollment);

            var command = new UpdateLessonProgressCommand { EnrollmentID = enrollmentId };

            // Act
            Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<ForbiddenException>()
                .WithMessage("Not authorized to update this enrollment.");
        }

        [Fact]
        public async Task Handle_ValidRequest_ShouldUpdateProgressAndCommitSuccessfully()
        {
            // Arrange
            var studentId = Guid.NewGuid();
            var enrollmentId = Guid.NewGuid();
            var chapterId = Guid.NewGuid();
            var lessonId = Guid.NewGuid();

            _mockCurrentUser.Setup(u => u.Id).Returns(studentId);

            var enrollment = new Enrollment(enrollmentId, studentId, Guid.NewGuid(), DateTime.UtcNow);

            _mockEnrollmentRepository
                .Setup(r => r.GetByIdAsync(enrollmentId))
                .ReturnsAsync(enrollment);

            var command = new UpdateLessonProgressCommand
            {
                EnrollmentID = enrollmentId,
                ChapterID = chapterId,
                LessonID = lessonId,
                IsCompleted = true
            };

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().Be(Unit.Value);

            _mockUnitOfWork.Verify(u => u.BeginTransactionAsync(), Times.Once);
            _mockEnrollmentRepository.Verify(r => r.UpsertLessonProgress(enrollmentId, chapterId, lessonId, true), Times.Once);
            _mockUnitOfWork.Verify(u => u.CommitAsync(studentId.ToString()), Times.Once);
        }
    }
}
