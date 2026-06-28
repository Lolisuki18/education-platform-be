using Application.BusinessException;
using Application.Features.Enrollments.Queries;
using Application.Results;
using Domain.Common.Interfaces;
using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Entity;
using Domain.EnrollmentManagement.Aggregate;
using Domain.EnrollmentManagement.Entity;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace UnitTests.Application.Features.Enrollments.Queries
{
    public class GetEnrollmentWeaknessQueryTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IEnrollmentRepository> _mockEnrollmentRepository;
        private readonly GetEnrollmentWeaknessQueryHandler _handler;

        public GetEnrollmentWeaknessQueryTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockEnrollmentRepository = new Mock<IEnrollmentRepository>();

            _mockUnitOfWork
                .Setup(u => u.GetRepository<IEnrollmentRepository>())
                .Returns(_mockEnrollmentRepository.Object);

            _handler = new GetEnrollmentWeaknessQueryHandler(_mockUnitOfWork.Object);
        }

        [Fact]
        public async Task Handle_EnrollmentDoesNotExist_ShouldThrowNotFoundException()
        {
            // Arrange
            var enrollmentId = Guid.NewGuid();
            _mockEnrollmentRepository
                .Setup(r => r.GetEnrollmentStatistic(enrollmentId))
                .ReturnsAsync((Enrollment?)null);

            var query = new GetEnrollmentWeaknessQuery { EnrollmentID = enrollmentId };

            // Act
            Func<Task> act = async () => await _handler.Handle(query, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<NotFound>()
                .WithMessage("Student statistic not found");
        }

        [Fact]
        public async Task Handle_NoCourseProgress_ShouldReturnEmptyWeaknessList()
        {
            // Arrange
            var enrollmentId = Guid.NewGuid();
            var enrollment = new Enrollment(enrollmentId, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

            // Set CourseProgress to null using reflection
            SetPrivateProperty(enrollment, nameof(Enrollment.CourseProgress), null!);

            _mockEnrollmentRepository
                .Setup(r => r.GetEnrollmentStatistic(enrollmentId))
                .ReturnsAsync(enrollment);

            var query = new GetEnrollmentWeaknessQuery { EnrollmentID = enrollmentId };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task Handle_NoFailedQuizzes_ShouldReturnEmptyWeaknessList()
        {
            // Arrange
            var enrollmentId = Guid.NewGuid();
            var enrollment = new Enrollment(enrollmentId, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

            var chapterProgress = enrollment.CourseProgress.AddChapterProgress(Guid.NewGuid());
            var lessonProgress = chapterProgress.AddLessonProgress(Guid.NewGuid());

            // Add a quiz that was answered correctly
            var quizProgress = lessonProgress.AddQuizProgress(Guid.NewGuid());
            quizProgress.RegisterAttempt(true);

            _mockEnrollmentRepository
                .Setup(r => r.GetEnrollmentStatistic(enrollmentId))
                .ReturnsAsync(enrollment);

            var query = new GetEnrollmentWeaknessQuery { EnrollmentID = enrollmentId };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task Handle_HasFailedQuizzes_ShouldReturnCalculatedWeaknessList()
        {
            // Arrange
            var enrollmentId = Guid.NewGuid();
            var studentId = Guid.NewGuid();
            var courseId = Guid.NewGuid();
            var chapterId = Guid.NewGuid();
            var lessonId = Guid.NewGuid();
            var quizId1 = Guid.NewGuid();
            var quizId2 = Guid.NewGuid();

            var enrollment = new Enrollment(enrollmentId, studentId, courseId, DateTime.UtcNow);

            var course = new Course(courseId, "Algebra Course", "Desc", 100m, "thumb.png", "algebra", "Prereq", "Outcomes", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
            SetPrivateProperty(enrollment, nameof(Enrollment.Course), course);

            var chapterProgress = enrollment.CourseProgress.AddChapterProgress(chapterId);
            var lessonProgress = chapterProgress.AddLessonProgress(lessonId);

            var lesson = new Lesson(lessonId, "Lesson 1: Intro", "Obj", "Desc", "video.mp4", 1, chapterId);
            SetPrivateProperty(lessonProgress, nameof(LessonProgress.Lesson), lesson);

            // Quiz 1: attempted and failed
            var quizProgress1 = lessonProgress.AddQuizProgress(quizId1);
            quizProgress1.RegisterAttempt(false);

            // Quiz 2: attempted and passed
            var quizProgress2 = lessonProgress.AddQuizProgress(quizId2);
            quizProgress2.RegisterAttempt(true);

            _mockEnrollmentRepository
                .Setup(r => r.GetEnrollmentStatistic(enrollmentId))
                .ReturnsAsync(enrollment);

            var query = new GetEnrollmentWeaknessQuery { EnrollmentID = enrollmentId };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().HaveCount(1);
            var weakness = result.First();
            weakness.CourseId.Should().Be(courseId);
            weakness.CourseTitle.Should().Be("Algebra Course");
            weakness.ChapterId.Should().Be(chapterId);
            weakness.LessonId.Should().Be(lessonId);
            weakness.LessonTitle.Should().Be("Lesson 1: Intro");
            weakness.FailedQuizCount.Should().Be(1);
            weakness.CompletionRate.Should().Be(0.5m); // 1 correct / 2 total quizzes
        }

        private void SetPrivateProperty(object target, string propertyName, object value)
        {
            var prop = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            prop?.SetValue(target, value);
        }
    }
}
