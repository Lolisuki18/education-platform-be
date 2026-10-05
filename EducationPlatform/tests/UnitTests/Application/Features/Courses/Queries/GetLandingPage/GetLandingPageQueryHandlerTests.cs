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
using Infrastructure.Persistence;
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

        /// <summary>
        /// The title search uses SQL LIKE, which the LINQ-to-objects mock cannot run, so these tests use the
        /// EF Core in-memory provider (it implements EF.Functions.Like).
        /// </summary>
        private async Task<(EducationPlatformDBContext Db, GetLandingPageQueryHandler Handler, Course Match, Course Other)> CreateDatabaseAsync(
            string matchTitle, string otherTitle)
        {
            var options = new DbContextOptionsBuilder<EducationPlatformDBContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var db = new EducationPlatformDBContext(options);

            var grade1 = new Grade(Guid.NewGuid(), "Grade 11");
            var grade2 = new Grade(Guid.NewGuid(), "Grade 12");
            var subject1 = new Subject(Guid.NewGuid(), "PHY11", "Physics 11", grade1.GradeID);
            var subject2 = new Subject(Guid.NewGuid(), "CHEM12", "Chemistry 12", grade2.GradeID);

            // The projection joins the teacher, so a course without one would silently drop out of the results
            var teacher = new Domain.IdentityManagement.Aggregate.User(
                Guid.NewGuid(), "teacher@example.com", "Password123", "0900000000", "Teacher", null,
                Domain.IdentityManagement.Enum.Role.Teacher, DateTime.UtcNow, true);
            db.Users.Add(teacher);

            var courseMatch = CreateCourseInstance(Guid.NewGuid(), teacher.UserID, CourseStatus.Published, matchTitle);
            SetPrivateProperty(courseMatch, nameof(Course.Grade), grade1);
            SetPrivateProperty(courseMatch, nameof(Course.Subject), subject1);

            var courseNoMatch = CreateCourseInstance(Guid.NewGuid(), teacher.UserID, CourseStatus.Published, otherTitle);
            SetPrivateProperty(courseNoMatch, nameof(Course.Grade), grade2);
            SetPrivateProperty(courseNoMatch, nameof(Course.Subject), subject2);

            db.Courses.AddRange(courseMatch, courseNoMatch);
            await db.SaveChangesAsync();

            return (db, new GetLandingPageQueryHandler(db, _mapper), courseMatch, courseNoMatch);
        }

        [Fact]
        public async Task Handle_QueryWithFilters_ShouldReturnFilteredPublishedCourses()
        {
            var (db, handler, courseMatch, _) = await CreateDatabaseAsync("Target Physics Course", "Other Chemistry Course");
            await using var _db = db;

            // Truy vấn lọc theo Title và GradeName
            var query = new GetLandingPageQuery
            {
                Title = "Physics",
                GradeName = "Grade 11"
            };

            var result = await handler.Handle(query, CancellationToken.None);

            result.Should().NotBeNull();
            result.Items.Should().HaveCount(1);
            result.Items.First().CourseID.Should().Be(courseMatch.CourseID);
            result.Items.First().Title.Should().Be("Target Physics Course");
            result.TotalItems.Should().Be(1);
        }

        [Theory]
        [InlineData("PHYSICS")]
        [InlineData("physics")]
        [InlineData("  sics Cou ")]
        public async Task Handle_TitleSearch_ShouldIgnoreCaseAndMatchAnywhereInTheTitle(string term)
        {
            var (db, handler, courseMatch, _) = await CreateDatabaseAsync("Target Physics Course", "Other Chemistry Course");
            await using var _db = db;

            var result = await handler.Handle(new GetLandingPageQuery { Title = term }, CancellationToken.None);

            result.Items.Should().ContainSingle().Which.CourseID.Should().Be(courseMatch.CourseID);
        }

        [Theory]
        [InlineData("%")]
        [InlineData("_")]
        [InlineData("100%")]
        public async Task Handle_TitleSearch_ShouldTreatWildcardCharactersAsPlainText(string term)
        {
            var (db, handler, _, _) = await CreateDatabaseAsync("Target Physics Course", "Other Chemistry Course");
            await using var _db = db;

            var result = await handler.Handle(new GetLandingPageQuery { Title = term }, CancellationToken.None);

            // Without escaping "%" would match every course
            result.Items.Should().BeEmpty();
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
