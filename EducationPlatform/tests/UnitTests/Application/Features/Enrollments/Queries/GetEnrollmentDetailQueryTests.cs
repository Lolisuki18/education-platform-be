using Application.Exceptions;
using Application.Features.Enrollments.Queries.GetEnrollmentDetail;
using Application.Interface;
using Application.Results;
using AutoMapper;
using Domain.Common.Interfaces;
using Domain.EnrollmentManagement.Aggregate;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace UnitTests.Application.Features.Enrollments.Queries.GetEnrollmentDetail
{
    public class GetEnrollmentDetailQueryTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IEnrollmentRepository> _mockEnrollmentRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<ICurrentUser> _mockCurrentUser;
        private readonly GetEnrollmentDetailQueryHandler _handler;

        public GetEnrollmentDetailQueryTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockEnrollmentRepository = new Mock<IEnrollmentRepository>();
            _mockMapper = new Mock<IMapper>();
            _mockCurrentUser = new Mock<ICurrentUser>();

            _mockUnitOfWork
                .Setup(u => u.GetRepository<IEnrollmentRepository>())
                .Returns(_mockEnrollmentRepository.Object);

            _handler = new GetEnrollmentDetailQueryHandler(
                _mockUnitOfWork.Object,
                _mockMapper.Object,
                _mockCurrentUser.Object);
        }

        [Fact]
        public async Task Handle_UserNotAuthenticated_ShouldThrowAuthenticateException()
        {
            // Arrange
            _mockCurrentUser.Setup(u => u.Id).Returns((Guid?)null);
            var query = new GetEnrollmentDetailQuery { EnrollmentID = Guid.NewGuid() };

            // Act
            Func<Task> act = async () => await _handler.Handle(query, CancellationToken.None);

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
                .Setup(r => r.GetEnrollmentDetailByID(enrollmentId))
                .ReturnsAsync((Enrollment?)null);

            var query = new GetEnrollmentDetailQuery { EnrollmentID = enrollmentId };

            // Act
            Func<Task> act = async () => await _handler.Handle(query, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<NotFoundException>()
                .WithMessage("Enrollment detail not found");
        }

        [Fact]
        public async Task Handle_UserNotOwnerAndNotAdmin_ShouldThrowForbiddenException()
        {
            // Arrange
            var studentId = Guid.NewGuid();
            var otherStudentId = Guid.NewGuid();
            var enrollmentId = Guid.NewGuid();

            _mockCurrentUser.Setup(u => u.Id).Returns(studentId);
            _mockCurrentUser.Setup(u => u.Role).Returns("Student");

            var enrollment = new Enrollment(enrollmentId, otherStudentId, Guid.NewGuid(), DateTime.UtcNow);

            _mockEnrollmentRepository
                .Setup(r => r.GetEnrollmentDetailByID(enrollmentId))
                .ReturnsAsync(enrollment);

            var query = new GetEnrollmentDetailQuery { EnrollmentID = enrollmentId };

            // Act
            Func<Task> act = async () => await _handler.Handle(query, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<ForbiddenException>()
                .WithMessage("You do not have permission to view this enrollment.");
        }

        [Fact]
        public async Task Handle_ValidOwnerRequest_ShouldReturnDetailAndHideQuizAnswers()
        {
            // Arrange
            var studentId = Guid.NewGuid();
            var enrollmentId = Guid.NewGuid();

            _mockCurrentUser.Setup(u => u.Id).Returns(studentId);
            _mockCurrentUser.Setup(u => u.Role).Returns("Student");

            var enrollment = new Enrollment(enrollmentId, studentId, Guid.NewGuid(), DateTime.UtcNow);

            _mockEnrollmentRepository
                .Setup(r => r.GetEnrollmentDetailByID(enrollmentId))
                .ReturnsAsync(enrollment);

            var expectedDto = CreateSampleDto(enrollmentId);
            _mockMapper.Setup(m => m.Map<EnrollmentDetailDTO>(enrollment)).Returns(expectedDto);

            var query = new GetEnrollmentDetailQuery { EnrollmentID = enrollmentId };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();

            // Xác thực rằng câu trả lời trắc nghiệm (CorrectAnswers) đã bị ẩn (xóa đi)
            var quizProgress = result.CourseProgress.ChapterProgresses.First().LessonProgresses.First().QuizProgresses.First();
            quizProgress.Quiz.Answer.CorrectAnswers.Should().BeEmpty();
        }

        [Fact]
        public async Task Handle_ValidAdminRequest_ShouldReturnDetailEvenIfNotOwnerAndHideQuizAnswers()
        {
            // Arrange
            var adminId = Guid.NewGuid();
            var studentId = Guid.NewGuid();
            var enrollmentId = Guid.NewGuid();

            _mockCurrentUser.Setup(u => u.Id).Returns(adminId);
            _mockCurrentUser.Setup(u => u.Role).Returns("Admin");

            var enrollment = new Enrollment(enrollmentId, studentId, Guid.NewGuid(), DateTime.UtcNow);

            _mockEnrollmentRepository
                .Setup(r => r.GetEnrollmentDetailByID(enrollmentId))
                .ReturnsAsync(enrollment);

            var expectedDto = CreateSampleDto(enrollmentId);
            _mockMapper.Setup(m => m.Map<EnrollmentDetailDTO>(enrollment)).Returns(expectedDto);

            var query = new GetEnrollmentDetailQuery { EnrollmentID = enrollmentId };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();

            // Kể cả Admin xem thì CorrectAnswers vẫn bị ẩn để đảm bảo tính công bằng (hoặc theo nghiệp vụ của code)
            var quizProgress = result.CourseProgress.ChapterProgresses.First().LessonProgresses.First().QuizProgresses.First();
            quizProgress.Quiz.Answer.CorrectAnswers.Should().BeEmpty();
        }

        private EnrollmentDetailDTO CreateSampleDto(Guid enrollmentId)
        {
            return new EnrollmentDetailDTO
            {
                EnrollmentID = enrollmentId,
                CourseProgress = new CourseProgressDTO
                {
                    ChapterProgresses = new List<ChapterProgressDTO>
                    {
                        new ChapterProgressDTO
                        {
                            LessonProgresses = new List<LessonProgressDTO>
                            {
                                new LessonProgressDTO
                                {
                                    QuizProgresses = new List<QuizProgressDTO>
                                    {
                                        new QuizProgressDTO
                                        {
                                            Quiz = new QuizDTO
                                            {
                                                Answer = new QuizAnswerDTO
                                                {
                                                    CorrectAnswers = new List<string> { "Answer A", "Answer B" }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };
        }
    }
}
