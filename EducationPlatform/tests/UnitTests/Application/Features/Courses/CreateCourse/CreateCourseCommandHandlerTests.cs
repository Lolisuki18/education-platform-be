using Application.Features.Courses.CreateCourse;
using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Entity;
using Domain.Common.Interfaces;
using Domain.CourseManagement.Events;
using Application.Interface;
using Application.BusinessException;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace UnitTests.Application.Features.Courses.CreateCourse
{
    public class CreateCourseCommandHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<ICourseRepository> _mockCourseRepository;
        private readonly Mock<IStorageService> _mockStorageService;
        private readonly Mock<ICurrentUser> _mockCurrentUser;
        private readonly CreateCourseCommandHandler _handler;

        public CreateCourseCommandHandlerTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockCourseRepository = new Mock<ICourseRepository>();
            _mockStorageService = new Mock<IStorageService>();
            _mockCurrentUser = new Mock<ICurrentUser>();

            _handler = new CreateCourseCommandHandler(
                _mockUnitOfWork.Object,
                _mockCourseRepository.Object,
                _mockStorageService.Object,
                _mockCurrentUser.Object);
        }

        [Fact]
        public async Task Handle_ValidRequest_ShouldCreateCourseAndCommit()
        {
            // Arrange
            var teacherId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(teacherId);

            var command = new CreateCourseCommand
            {
                Title = "Test Course",
                Description = "A course for testing",
                Price = 100,
                ThumbnailName = "thumbnail.png",
                Slug = "test-course",
                Prerequisites = "None",
                LearningOutcomes = "Learn testing",
                GradeID = Guid.NewGuid(),
                SubjectID = Guid.NewGuid(),
                Chapters = new List<CreateChapterCommandDto>()
            };

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().NotBeEmpty();

            _mockUnitOfWork.Verify(u => u.BeginTransactionAsync(), Times.Once);

            _mockCourseRepository.Verify(r => r.Add(It.Is<Course>(c =>
                c.Title == command.Title &&
                c.Price.Amount == command.Price &&
                c.TeacherID == teacherId &&
                c.DomainEvents.Any(e => e is CourseCreatedEvent))), Times.Once);

            _mockUnitOfWork.Verify(u => u.CommitAsync(teacherId.ToString()), Times.Once);
        }

        [Fact]
        public async Task Handle_UserNotAuthenticated_ShouldThrowAuthenticateException()
        {
            // Arrange
            _mockCurrentUser.Setup(u => u.Id).Returns((Guid?)null);
            var command = new CreateCourseCommand { Title = "Unauthorized Course" };

            // Act
            Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<AuthenticateException>()
                .WithMessage("User must be authenticated.");
        }

        [Fact]
        public async Task Handle_ValidRequestWithThumbnailFile_ShouldSaveFileAndCreateCourse()
        {
            // Arrange
            var teacherId = Guid.NewGuid();
            using var dummyStream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00 });
            _mockCurrentUser.Setup(u => u.Id).Returns(teacherId);

            var command = new CreateCourseCommand
            {
                Title = "Test Course with File",
                Description = "A course for testing file upload",
                Price = 50,
                ThumbnailName = "old_name.png",
                ThumbnailFileStream = dummyStream,
                ThumbnailFileExtension = "jpg",
                Slug = "file-course",
                Prerequisites = "None",
                LearningOutcomes = "Learn file upload",
                GradeID = Guid.NewGuid(),
                SubjectID = Guid.NewGuid(),
                Chapters = new List<CreateChapterCommandDto>()
            };

            _mockStorageService
                .Setup(s => s.SaveAsync(dummyStream, "jpg", It.IsAny<CancellationToken>()))
                .ReturnsAsync("storage/cover_12345.jpg");

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().NotBeEmpty();

            _mockStorageService.Verify(s => s.SaveAsync(dummyStream, "jpg", It.IsAny<CancellationToken>()), Times.Once);
            _mockCourseRepository.Verify(r => r.Add(It.Is<Course>(c =>
                c.Title == command.Title &&
                c.ThumbnailName == "storage/cover_12345.jpg")), Times.Once);
            _mockUnitOfWork.Verify(u => u.CommitAsync(teacherId.ToString()), Times.Once);
        }

        [Fact]
        public async Task Handle_ExceptionDuringCommitWithThumbnailFile_ShouldDeleteSavedFileAndRethrow()
        {
            // Arrange
            var teacherId = Guid.NewGuid();
            using var dummyStream = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x00, 0x00, 0x00, 0x00 });
            _mockCurrentUser.Setup(u => u.Id).Returns(teacherId);

            var command = new CreateCourseCommand
            {
                Title = "Failing Course with File",
                Description = "A course designed to fail on commit",
                Price = 50,
                ThumbnailName = "old_name.png",
                ThumbnailFileStream = dummyStream,
                ThumbnailFileExtension = "png",
                Slug = "failing-course",
                Prerequisites = "None",
                LearningOutcomes = "Test rollback",
                GradeID = Guid.NewGuid(),
                SubjectID = Guid.NewGuid(),
                Chapters = new List<CreateChapterCommandDto>()
            };

            _mockStorageService
                .Setup(s => s.SaveAsync(dummyStream, "png", It.IsAny<CancellationToken>()))
                .ReturnsAsync("storage/failed_cover.png");

            _mockUnitOfWork
                .Setup(u => u.CommitAsync(teacherId.ToString()))
                .ThrowsAsync(new Exception("Database connection lost"));

            // Act
            Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<Exception>().WithMessage("Database connection lost");

            // Verify that the uploaded file was cleaned up (deleted)
            _mockStorageService.Verify(s => s.DeleteAsync("storage/failed_cover.png"), Times.Once);
        }
    }
}
