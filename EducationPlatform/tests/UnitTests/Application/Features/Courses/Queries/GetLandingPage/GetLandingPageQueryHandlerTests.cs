using Application;
using Application.Features.Academic.Queries.GetGrades;
using Application.Features.Academic.Queries.GetSubjects;
using Application.Features.Courses.Queries.GetLandingPage;
using Application.Interface;
using Application.Results;
using AutoMapper;
using Domain.AcademicManagement.Aggregate;
using Domain.AcademicManagement.Entity;
using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Enum;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

using Microsoft.Extensions.Logging;

namespace UnitTests.Application.Features.Courses.Queries.GetLandingPage
{
    public class GetLandingPageQueryHandlerTests
    {
        private readonly IMapper _mapper;
        private readonly Mock<IApplicationDBContext> _mockContext;
        private readonly GetLandingPageQueryHandler _handler;

        public GetLandingPageQueryHandlerTests()
        {
            var mockLoggerFactory = new Mock<ILoggerFactory>();
            var mockLogger = new Mock<ILogger>();
            mockLoggerFactory
                .Setup(f => f.CreateLogger(It.IsAny<string>()))
                .Returns(mockLogger.Object);

            // Thiết lập cấu hình Mapper thật từ Assembly của Application để dùng ProjectTo
            var mapperConfig = new MapperConfiguration(cfg =>
            {
                cfg.AddMaps(typeof(ApplicationDI).Assembly);
            }, mockLoggerFactory.Object);
            _mapper = mapperConfig.CreateMapper();

            _mockContext = new Mock<IApplicationDBContext>();
            _handler = new GetLandingPageQueryHandler(_mockContext.Object, _mapper);
        }

        [Fact]
        public async Task Handle_DefaultQuery_ShouldReturnPublishedCoursesGradesAndSubjects()
        {
            // Arrange
            var grade = new Grade(Guid.NewGuid(), "Grade 10");
            var subject = new Subject(Guid.NewGuid(), "MATH10", "Math 10", grade.GradeID);

            var course1 = CreateCourseInstance(Guid.NewGuid(), Guid.NewGuid(), CourseStatus.Published);
            SetPrivateProperty(course1, nameof(Course.Grade), grade);
            SetPrivateProperty(course1, nameof(Course.Subject), subject);

            // Khóa học nháp - không được xuất hiện trên landing page
            var draftCourse = CreateCourseInstance(Guid.NewGuid(), Guid.NewGuid(), CourseStatus.InReview);

            var coursesList = new List<Course> { course1, draftCourse };
            var gradesList = new List<Grade> { grade };
            var subjectsList = new List<Subject> { subject };

            // Sử dụng helper để mock DbSet hỗ trợ async IQueryable
            var mockCoursesDbSet = DbSetMockHelper.CreateMockDbSet(coursesList);
            var mockGradesDbSet = DbSetMockHelper.CreateMockDbSet(gradesList);
            var mockSubjectsDbSet = DbSetMockHelper.CreateMockDbSet(subjectsList);

            _mockContext.Setup(c => c.Courses).Returns(mockCoursesDbSet.Object);

            var query = new GetLandingPageQuery();

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Items.Should().HaveCount(1); // Chỉ lấy course đã published
            result.Items.First().CourseID.Should().Be(course1.CourseID);
            result.PageIndex.Should().Be(1);
            result.PageSize.Should().Be(10);
            result.TotalItems.Should().Be(1);
        }

        [Fact]
        public async Task Handle_QueryWithFilters_ShouldReturnFilteredPublishedCourses()
        {
            // Arrange
            var grade1 = new Grade(Guid.NewGuid(), "Grade 11");
            var grade2 = new Grade(Guid.NewGuid(), "Grade 12");
            var subject1 = new Subject(Guid.NewGuid(), "PHY11", "Physics 11", grade1.GradeID);
            var subject2 = new Subject(Guid.NewGuid(), "CHEM12", "Chemistry 12", grade2.GradeID);

            var courseMatch = CreateCourseInstance(Guid.NewGuid(), Guid.NewGuid(), CourseStatus.Published, "Target Physics Course");
            SetPrivateProperty(courseMatch, nameof(Course.Grade), grade1);
            SetPrivateProperty(courseMatch, nameof(Course.Subject), subject1);

            var courseNoMatch = CreateCourseInstance(Guid.NewGuid(), Guid.NewGuid(), CourseStatus.Published, "Other Chemistry Course");
            SetPrivateProperty(courseNoMatch, nameof(Course.Grade), grade2);
            SetPrivateProperty(courseNoMatch, nameof(Course.Subject), subject2);

            var coursesList = new List<Course> { courseMatch, courseNoMatch };

            var mockCoursesDbSet = DbSetMockHelper.CreateMockDbSet(coursesList);

            _mockContext.Setup(c => c.Courses).Returns(mockCoursesDbSet.Object);

            // Truy vấn lọc theo Title và GradeName
            var query = new GetLandingPageQuery
            {
                Title = "Physics",
                GradeName = "Grade 11"
            };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Items.Should().HaveCount(1);
            result.Items.First().CourseID.Should().Be(courseMatch.CourseID);
            result.Items.First().Title.Should().Be("Target Physics Course");
            result.TotalItems.Should().Be(1);
        }

        private void SetPrivateProperty(object target, string propertyName, object value)
        {
            var prop = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            prop?.SetValue(target, value);
        }

        private Course CreateCourseInstance(Guid courseId, Guid teacherId, CourseStatus status, string title = "Test Course")
        {
            var course = new Course(
                courseId,
                title,
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

            var statusProp = typeof(Course).GetProperty(nameof(Course.Status));
            statusProp?.SetValue(course, status);

            var publishedProp = typeof(Course).GetProperty(nameof(Course.PublishedAt));
            publishedProp?.SetValue(course, DateTime.UtcNow);

            return course;
        }
    }
}
