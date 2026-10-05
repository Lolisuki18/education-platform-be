using Application.Exceptions;
using Application.Features.Courses.Queries.GetCourses;
using Application.Interface;
using Application.Results;
using AutoMapper;
using Domain.Common.Interfaces;
using Domain.CourseManagement.Aggregate;
using Domain.IdentityManagement.Enum;
using Domain.IdentityManagement.ValueObject;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace UnitTests.Application.Features.Courses.Queries.GetCourses
{
    public class GetCoursesQueryHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<ICourseRepository> _mockCourseRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<ICurrentUser> _mockCurrentUser;
        private readonly GetCoursesQueryHandler _handler;

        public GetCoursesQueryHandlerTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockCourseRepository = new Mock<ICourseRepository>();
            _mockMapper = new Mock<IMapper>();
            _mockCurrentUser = new Mock<ICurrentUser>();

            _mockUnitOfWork
                .Setup(u => u.GetRepository<ICourseRepository>())
                .Returns(_mockCourseRepository.Object);

            _handler = new GetCoursesQueryHandler(
                _mockUnitOfWork.Object,
                _mockMapper.Object,
                _mockCurrentUser.Object);
        }

        [Fact]
        public async Task Handle_InvalidRole_ShouldThrowAuthenticateException()
        {
            // Arrange
            var query = new GetCoursesQuery();
            _mockCurrentUser.Setup(u => u.IsAuthenticated).Returns(true);
            _mockCurrentUser.Setup(u => u.Role).Returns("NonExistentRole");

            // Act
            Func<Task> act = async () => await _handler.Handle(query, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<AuthenticateException>().WithMessage("Invalid role");
        }

        [Fact]
        public async Task Handle_CourseListIsEmptyOrNull_ShouldThrowNotFoundException()
        {
            // Arrange
            var query = new GetCoursesQuery();
            _mockCurrentUser.Setup(u => u.IsAuthenticated).Returns(false);

            var courses = new List<Course>();
            _mockCourseRepository
                .Setup(r => r.GetAllCourses(
                    query.Title,
                    query.Price,
                    query.TeacherName,
                    query.GradeName,
                    query.SubjectName,
                    query.PageIndex,
                    query.PageSize,
                    null,
                    null))
                .ReturnsAsync(courses);

            _mockMapper
                .Setup(m => m.Map<List<CourseDTO>>(courses))
                .Returns(new List<CourseDTO>());

            // Act
            Func<Task> act = async () => await _handler.Handle(query, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<NotFoundException>().WithMessage("Course list is not found or empty");
        }

        [Fact]
        public async Task Handle_ValidRequestAsAnonymous_ShouldReturnAllPublishedCourses()
        {
            // Arrange
            var query = new GetCoursesQuery
            {
                Title = "Math",
                Price = 50,
                PageIndex = 1,
                PageSize = 5
            };
            _mockCurrentUser.Setup(u => u.IsAuthenticated).Returns(false);

            var course = CreateCourseInstance(Guid.NewGuid(), Guid.NewGuid());
            var coursesList = new List<Course> { course };

            _mockCourseRepository
                .Setup(r => r.GetAllCourses(
                    query.Title,
                    query.Price,
                    query.TeacherName,
                    query.GradeName,
                    query.SubjectName,
                    query.PageIndex,
                    query.PageSize,
                    null,
                    null))
                .ReturnsAsync(coursesList);

            var expectedDtos = new List<CourseDTO>
            {
                new CourseDTO { CourseID = course.CourseID, Title = "Test Course" }
            };

            _mockMapper
                .Setup(m => m.Map<List<CourseDTO>>(coursesList))
                .Returns(expectedDtos);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(expectedDtos);

            _mockCourseRepository.Verify(r => r.GetAllCourses(
                query.Title,
                query.Price,
                query.TeacherName,
                query.GradeName,
                query.SubjectName,
                query.PageIndex,
                query.PageSize,
                null,
                null), Times.Once);
        }

        [Fact]
        public async Task Handle_ValidRequestAsTeacher_ShouldReturnOnlyTeacherCourses()
        {
            // Arrange
            var teacherId = Guid.NewGuid();
            var query = new GetCoursesQuery
            {
                Title = "Chemistry",
                PageIndex = 1,
                PageSize = 10
            };

            _mockCurrentUser.Setup(u => u.IsAuthenticated).Returns(true);
            _mockCurrentUser.Setup(u => u.Role).Returns(Role.Teacher.ToString());
            _mockCurrentUser.Setup(u => u.Id).Returns(teacherId);

            var course = CreateCourseInstance(Guid.NewGuid(), teacherId);
            var coursesList = new List<Course> { course };

            _mockCourseRepository
                .Setup(r => r.GetAllCourses(
                    query.Title,
                    query.Price,
                    query.TeacherName,
                    query.GradeName,
                    query.SubjectName,
                    query.PageIndex,
                    query.PageSize,
                    teacherId,
                    Role.Teacher))
                .ReturnsAsync(coursesList);

            var expectedDtos = new List<CourseDTO>
            {
                new CourseDTO { CourseID = course.CourseID, Title = "Test Course" }
            };

            _mockMapper
                .Setup(m => m.Map<List<CourseDTO>>(coursesList))
                .Returns(expectedDtos);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(expectedDtos);

            _mockCourseRepository.Verify(r => r.GetAllCourses(
                query.Title,
                query.Price,
                query.TeacherName,
                query.GradeName,
                query.SubjectName,
                query.PageIndex,
                query.PageSize,
                teacherId,
                Role.Teacher), Times.Once);
        }

        private Course CreateCourseInstance(Guid courseId, Guid teacherId)
        {
            return new Course(
                courseId,
                "Test Course",
                "Description",
                100,
                "thumbnail.png",
                "test-course",
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
