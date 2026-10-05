using Application.Exceptions;
using Application.Features.Enrollments.Commands;
using Application.Interface;
using Domain.Common.Interfaces;
using Domain.EnrollmentManagement.Aggregate;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace UnitTests.Application.Features.Enrollments.Commands
{
    public class SubmitQuizCommandHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IEnrollmentRepository> _mockEnrollmentRepository;
        private readonly Mock<ICurrentUser> _mockCurrentUser;
        private readonly SubmitQuizCommandHandler _handler;

        public SubmitQuizCommandHandlerTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockEnrollmentRepository = new Mock<IEnrollmentRepository>();
            _mockCurrentUser = new Mock<ICurrentUser>();

            _mockUnitOfWork
                .Setup(u => u.GetRepository<IEnrollmentRepository>())
                .Returns(_mockEnrollmentRepository.Object);

            _handler = new SubmitQuizCommandHandler(
                _mockUnitOfWork.Object,
                _mockCurrentUser.Object);
        }

        [Fact]
        public async Task Handle_UserNotAuthenticated_ShouldThrowAuthenticateException()
        {
            // Arrange
            _mockCurrentUser.Setup(u => u.Id).Returns((Guid?)null);
            var command = new SubmitQuizCommand { EnrollmentID = Guid.NewGuid() };

            // Act
            Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<AuthenticateException>()
                .WithMessage("User must be authenticated.");
        }

        [Fact]
        public async Task Handle_EnrollmentDoesNotExist_ShouldThrowNotFoundException()
        {
            // Arrange
            var studentId = Guid.NewGuid();
            var enrollmentId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(studentId);

            _mockEnrollmentRepository
                .Setup(r => r.GetEnrollmentForUpdate(enrollmentId))
                .ReturnsAsync((Enrollment?)null);

            var command = new SubmitQuizCommand { EnrollmentID = enrollmentId };

            // Act
            Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<NotFoundException>()
                .WithMessage("Enrollment not found");
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
                .Setup(r => r.GetEnrollmentForUpdate(enrollmentId))
                .ReturnsAsync(enrollment);

            var command = new SubmitQuizCommand { EnrollmentID = enrollmentId };

            // Act
            Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<ForbiddenException>()
                .WithMessage("You are not the owner of this enrollment");
        }

        [Fact]
        public async Task Handle_ValidRequest_ShouldSubmitQuizAndReturnResult()
        {
            // Arrange
            var studentId = Guid.NewGuid();
            var enrollmentId = Guid.NewGuid();
            var chapterId = Guid.NewGuid();
            var lessonId = Guid.NewGuid();
            var quizId = Guid.NewGuid();
            var answers = new List<string> { "A", "B" };

            _mockCurrentUser.Setup(u => u.Id).Returns(studentId);

            var enrollment = new Enrollment(enrollmentId, studentId, Guid.NewGuid(), DateTime.UtcNow);

            _mockEnrollmentRepository
                .Setup(r => r.GetEnrollmentForUpdate(enrollmentId))
                .ReturnsAsync(enrollment);

            _mockEnrollmentRepository
                .Setup(r => r.UpsertQuizProgress(enrollmentId, chapterId, lessonId, quizId, answers))
                .ReturnsAsync((isCorrect: true, correctAnswers: new List<string> { "A", "B" }, explanation: "Well explained"));

            var command = new SubmitQuizCommand
            {
                EnrollmentID = enrollmentId,
                ChapterID = chapterId,
                LessonID = lessonId,
                QuizID = quizId,
                SelectedAnswers = answers
            };

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.IsCorrect.Should().BeTrue();
            result.Explanation.Should().Be("Well explained");

            _mockUnitOfWork.Verify(u => u.BeginTransactionAsync(), Times.Once);
            _mockEnrollmentRepository.Verify(r => r.UpsertQuizProgress(enrollmentId, chapterId, lessonId, quizId, answers), Times.Once);
            _mockUnitOfWork.Verify(u => u.CommitAsync(studentId.ToString()), Times.Once);
        }
    }
}
