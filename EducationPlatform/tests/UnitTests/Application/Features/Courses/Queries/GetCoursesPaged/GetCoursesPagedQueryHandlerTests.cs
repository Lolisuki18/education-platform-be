using Application;
using Application.Features.Courses.Queries.GetCoursesPaged;
using Application.Interface;
using AutoMapper;
using Domain.AcademicManagement.Aggregate;
using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Enum;
using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.Enum;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace UnitTests.Application.Features.Courses.Queries.GetCoursesPaged
{
    public class GetCoursesPagedQueryHandlerTests
    {
        private readonly EducationPlatformDBContext _db;
        private readonly Mock<ICurrentUser> _currentUser = new();
        private readonly GetCoursesPagedQueryHandler _handler;
        private readonly Grade _grade = new(Guid.NewGuid(), "Grade 10");
        private readonly Subject _subject;
        private readonly User _teacherA;
        private readonly User _teacherB;

        public GetCoursesPagedQueryHandlerTests()
        {
            _db = new EducationPlatformDBContext(
                new DbContextOptionsBuilder<EducationPlatformDBContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

            var mapper = new MapperConfiguration(cfg => cfg.AddMaps(typeof(ApplicationDI).Assembly), NullLoggerFactory.Instance).CreateMapper();
            _handler = new GetCoursesPagedQueryHandler(_db, mapper, _currentUser.Object);

            _subject = new Subject(Guid.NewGuid(), "MATH10", "Math 10", _grade.GradeID);
            _teacherA = new User(Guid.NewGuid(), "a@example.com", "Secret123", "0901111111", "Teacher A", null, Role.Teacher, DateTime.UtcNow, true);
            _teacherB = new User(Guid.NewGuid(), "b@example.com", "Secret123", "0902222222", "Teacher B", null, Role.Teacher, DateTime.UtcNow, true);
            _db.Grades.Add(_grade);
            _db.Subjects.Add(_subject);
            _db.Users.AddRange(_teacherA, _teacherB);
        }

        private Course AddCourse(User teacher, string title, CourseStatus status, DateTime createdAt)
        {
            var course = new Course(Guid.NewGuid(), title, "Description", 100m, "thumb.png", null, "none", "outcomes",
                teacher.UserID, _grade.GradeID, _subject.SubjectID, createdAt);
            _db.Courses.Add(course);
            _db.Entry(course).Property(c => c.Status).CurrentValue = status;
            return course;
        }

        private async Task<IReadOnlyList<string>> Titles(GetCoursesPagedQuery query)
        {
            await _db.SaveChangesAsync();
            var result = await _handler.Handle(query, CancellationToken.None);
            return result.Items.Select(i => i.Title).ToList();
        }

        private void SignInAs(string role, Guid? id = null)
        {
            _currentUser.Setup(c => c.IsAuthenticated).Returns(true);
            _currentUser.Setup(c => c.Role).Returns(role);
            _currentUser.Setup(c => c.Id).Returns(id);
        }

        [Fact]
        public async Task NewestCoursesComeFirst_AndThePageReportsTheTotal()
        {
            AddCourse(_teacherA, "Oldest", CourseStatus.Published, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            AddCourse(_teacherA, "Newest", CourseStatus.Published, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));
            AddCourse(_teacherA, "Middle", CourseStatus.Published, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
            SignInAs("Admin");
            await _db.SaveChangesAsync();

            var result = await _handler.Handle(new GetCoursesPagedQuery(), CancellationToken.None);

            result.Items.Select(i => i.Title).Should().Equal("Newest", "Middle", "Oldest");
            result.TotalItems.Should().Be(3);
        }

        [Fact]
        public async Task PagingSkipsAndTakes()
        {
            for (var i = 1; i <= 5; i++)
                AddCourse(_teacherA, $"Course {i}", CourseStatus.Published, new DateTime(2026, 1, i, 0, 0, 0, DateTimeKind.Utc));
            SignInAs("Admin");
            await _db.SaveChangesAsync();

            var result = await _handler.Handle(new GetCoursesPagedQuery { PageIndex = 2, PageSize = 2 }, CancellationToken.None);

            result.Items.Select(i => i.Title).Should().Equal("Course 3", "Course 2");
            result.TotalItems.Should().Be(5);
            result.PageIndex.Should().Be(2);
            result.PageSize.Should().Be(2);
        }

        [Fact]
        public async Task ATeacherSeesOnlyTheirOwnCourses()
        {
            AddCourse(_teacherA, "From A", CourseStatus.Published, DateTime.UtcNow);
            AddCourse(_teacherB, "From B", CourseStatus.Published, DateTime.UtcNow);
            SignInAs("Teacher", _teacherA.UserID);

            (await Titles(new GetCoursesPagedQuery())).Should().Equal("From A");
        }

        [Fact]
        public async Task AnAdminSeesEveryTeachersCourses()
        {
            AddCourse(_teacherA, "From A", CourseStatus.Published, DateTime.UtcNow);
            AddCourse(_teacherB, "From B", CourseStatus.Published, DateTime.UtcNow);
            SignInAs("Admin");

            (await Titles(new GetCoursesPagedQuery())).Should().BeEquivalentTo("From A", "From B");
        }

        [Fact]
        public async Task TheTitleFilter_IgnoresCase_AndTreatsPercentAsAPlainCharacter()
        {
            AddCourse(_teacherA, "Algebra basics", CourseStatus.Published, DateTime.UtcNow);
            AddCourse(_teacherA, "Geometry", CourseStatus.Published, DateTime.UtcNow);
            AddCourse(_teacherA, "100% Physics", CourseStatus.Published, DateTime.UtcNow);
            SignInAs("Admin");

            (await Titles(new GetCoursesPagedQuery { Title = "ALGEBRA" })).Should().Equal("Algebra basics");
            (await Titles(new GetCoursesPagedQuery { Title = "%" })).Should().Equal("100% Physics");
        }

        [Theory]
        [InlineData("Pending", "InReviewCourse")]
        [InlineData("InReview", "InReviewCourse")]
        [InlineData("approved", "PublishedCourse")]
        [InlineData("Published", "PublishedCourse")]
        [InlineData("Rejected", "RejectedCourse")]
        public async Task TheStatusFilter_UnderstandsTheFrontendLabels(string status, string expectedTitle)
        {
            AddCourse(_teacherA, "InReviewCourse", CourseStatus.InReview, DateTime.UtcNow);
            AddCourse(_teacherA, "PublishedCourse", CourseStatus.Published, DateTime.UtcNow);
            AddCourse(_teacherA, "RejectedCourse", CourseStatus.Rejected, DateTime.UtcNow);
            SignInAs("Admin");

            (await Titles(new GetCoursesPagedQuery { Status = status })).Should().Equal(expectedTitle);
        }

        [Fact]
        public async Task AnUnknownStatus_IsIgnored_InsteadOfHidingEverything()
        {
            AddCourse(_teacherA, "One", CourseStatus.Published, DateTime.UtcNow);
            AddCourse(_teacherA, "Two", CourseStatus.Rejected, DateTime.UtcNow);
            SignInAs("Admin");

            (await Titles(new GetCoursesPagedQuery { Status = "no-such-status" })).Should().HaveCount(2);
        }

        [Fact]
        public void PagingValuesFromTheClient_AreKeptInsideTheirBounds()
        {
            var query = new GetCoursesPagedQuery { PageIndex = -5, PageSize = 100000 };

            query.PageIndex.Should().Be(1);
            query.PageSize.Should().Be(100);
            new GetCoursesPagedQuery { PageSize = 0 }.PageSize.Should().Be(10);
        }
    }
}
