using Application.Features.Courses.CreateCourse;
using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Entity;
using Domain.Common.Interfaces;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Domain.CourseManagement.Events;
using Application.Interface;

namespace UnitTests.Application.Features.Courses.CreateCourse
{
    public class CreateCourseCommandHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<ICourseRepository> _mockCourseRepository;
        private readonly Mock<IStorageService> _mockStorageService;
        private readonly CreateCourseCommandHandler _handler;

        public CreateCourseCommandHandlerTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockCourseRepository = new Mock<ICourseRepository>();
            _mockStorageService = new Mock<IStorageService>();

            _handler = new CreateCourseCommandHandler(
                _mockUnitOfWork.Object,
                _mockCourseRepository.Object,
                _mockStorageService.Object);
        }

        [Fact]
        public async Task Handle_ValidRequest_ShouldCreateCourseAndCommit()
        {
            // Arrange
            var teacherId = Guid.NewGuid();
            var command = new CreateCourseCommand
            {
                Title = "Test Course",
                Description = "A course for testing",
                Price = 100,
                ThumbnailName = "thumbnail.png",
                Slug = "test-course",
                Prerequisites = "None",
                LearningOutcomes = "Learn testing",
                CallerId = teacherId,
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
                c.TeacherID == command.CallerId &&
                c.DomainEvents.Any(e => e is CourseCreatedEvent))), Times.Once);

            _mockCourseRepository.Verify(r => r.AddChapters(It.IsAny<IEnumerable<Chapter>>()), Times.Once);
            _mockCourseRepository.Verify(r => r.AddLessons(It.IsAny<IEnumerable<Lesson>>()), Times.Once);
            
            _mockUnitOfWork.Verify(u => u.CommitAsync(teacherId.ToString()), Times.Once);
        }
    }
}
