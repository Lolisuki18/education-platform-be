using Application.BusinessException;
using Application.Features.Courses.ReviewCourse;
using Application.Interface;
using Domain.Common.Interfaces;
using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Entity;
using Domain.CourseManagement.Enum;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace UnitTests.Application.Features.Courses.ReviewCourse
{
    public class ReviewCourseCommandHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<ICourseRepository> _mockCourseRepository;
        private readonly Mock<ICurrentUser> _mockCurrentUser;
        private readonly ReviewCourseCommandHandler _handler;

        public ReviewCourseCommandHandlerTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockCourseRepository = new Mock<ICourseRepository>();
            _mockCurrentUser = new Mock<ICurrentUser>();

            _mockUnitOfWork
                .Setup(u => u.GetRepository<ICourseRepository>())
                .Returns(_mockCourseRepository.Object);

            _handler = new ReviewCourseCommandHandler(
                _mockUnitOfWork.Object,
                _mockCurrentUser.Object);
        }

        [Fact]
        public async Task Handle_UserNotAuthenticated_ShouldThrowAuthenticateException()
        {
            // Arrange
            _mockCurrentUser.Setup(u => u.Id).Returns((Guid?)null);
            var command = new ReviewCourseCommand { CourseID = Guid.NewGuid() };

            // Act
            Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<AuthenticateException>()
                .WithMessage("User must be authenticated.");
        }

        [Fact]
        public async Task Handle_CourseNotFound_ShouldThrowNotFoundException()
        {
            // Arrange
            var adminId = Guid.NewGuid();
            var courseId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(adminId);

            _mockCourseRepository
                .Setup(r => r.GetCourseDetailByID(courseId))
                .ReturnsAsync((Course?)null);

            var command = new ReviewCourseCommand { CourseID = courseId };

            // Act
            Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<NotFound>()
                .WithMessage($"Course with ID: {courseId} is not found");
        }

        [Fact]
        public async Task Handle_ApproveCourse_ShouldPublishCourseAndCommitTransaction()
        {
            // Arrange
            var adminId = Guid.NewGuid();
            var courseId = Guid.NewGuid();
            var teacherId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(adminId);

            var course = CreateCourseInstance(courseId, teacherId);
            _mockCourseRepository
                .Setup(r => r.GetCourseDetailByID(courseId))
                .ReturnsAsync(course);

            var command = new ReviewCourseCommand
            {
                CourseID = courseId,
                ViolatedPolicyIDs = null,
                ViolatedChapters = null,
                AdminNote = "Excellent course!"
            };

            // Act
            await _handler.Handle(command, CancellationToken.None);

            // Assert
            course.Status.Should().Be(CourseStatus.Published);
            course.AdminNote.Should().Be("Excellent course!");

            _mockUnitOfWork.Verify(u => u.BeginTransactionAsync(), Times.Once);
            _mockCourseRepository.Verify(r => r.UpdateAsync(courseId, course, It.IsAny<CancellationToken>()), Times.Once);
            _mockCourseRepository.Verify(r => r.ReplaceViolatedPolicies(courseId, It.Is<IEnumerable<ViolatedPolicy>>(l => !l.Any())), Times.Once);
            _mockUnitOfWork.Verify(u => u.CommitAsync(adminId.ToString()), Times.Once);
        }

        [Fact]
        public async Task Handle_RejectCourse_ShouldRejectCourseAndReplaceViolatedPolicies()
        {
            // Arrange
            var adminId = Guid.NewGuid();
            var courseId = Guid.NewGuid();
            var teacherId = Guid.NewGuid();
            var policyId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(adminId);

            var course = CreateCourseInstance(courseId, teacherId);
            _mockCourseRepository
                .Setup(r => r.GetCourseDetailByID(courseId))
                .ReturnsAsync(course);

            var command = new ReviewCourseCommand
            {
                CourseID = courseId,
                ViolatedPolicyIDs = new List<Guid> { policyId },
                ViolatedChapters = null,
                AdminNote = "Violates basic instructional guidelines."
            };

            // Act
            await _handler.Handle(command, CancellationToken.None);

            // Assert
            course.Status.Should().Be(CourseStatus.Rejected);
            course.AdminNote.Should().Be("Violates basic instructional guidelines.");

            _mockUnitOfWork.Verify(u => u.BeginTransactionAsync(), Times.Once);
            _mockCourseRepository.Verify(r => r.UpdateAsync(courseId, course, It.IsAny<CancellationToken>()), Times.Once);
            _mockCourseRepository.Verify(r => r.ReplaceViolatedPolicies(courseId, It.Is<IEnumerable<ViolatedPolicy>>(list =>
                list.Count() == 1 && list.First().PolicyID == policyId
            )), Times.Once);
            _mockUnitOfWork.Verify(u => u.CommitAsync(adminId.ToString()), Times.Once);
        }

        private Course CreateCourseInstance(Guid courseId, Guid teacherId)
        {
            return new Course(
                courseId,
                "Submission Course",
                "Needs review",
                150m,
                "thumb.png",
                "submission-course",
                "Prerequisites",
                "Outcomes",
                teacherId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                DateTime.UtcNow
            );
        }
    }
}
