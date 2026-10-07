using Application.Exceptions;
using Application.Features.Statistics.Queries.GetTeacherSummary;
using Application.Interface;
using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Entity;
using Domain.CourseManagement.Enum;
using Domain.EnrollmentManagement.Aggregate;
using Domain.OrderManagement.Aggregate;
using Domain.OrderManagement.ValueObject;
using FluentAssertions;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace UnitTests.Application.Features.Statistics
{
    public class GetTeacherSummaryQueryHandlerTests
    {
        private readonly EducationPlatformDBContext _db;
        private readonly Mock<ICurrentUser> _currentUser = new();
        private readonly Guid _teacherId = Guid.NewGuid();
        private readonly GetTeacherSummaryQueryHandler _handler;

        public GetTeacherSummaryQueryHandlerTests()
        {
            _db = new EducationPlatformDBContext(
                new DbContextOptionsBuilder<EducationPlatformDBContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

            _currentUser.Setup(c => c.Id).Returns(_teacherId);
            _handler = new GetTeacherSummaryQueryHandler(_db, _currentUser.Object);
        }

        private Course AddCourse(Guid teacherId, string title, CourseStatus status)
        {
            var course = new Course(Guid.NewGuid(), title, "Description", 100m, "thumb.png", null, "none", "outcomes", teacherId, Guid.NewGuid(), Guid.NewGuid(), null);
            _db.Courses.Add(course);
            _db.Entry(course).Property(c => c.Status).CurrentValue = status;
            return course;
        }

        private void AddPaidOrder(Course course, decimal total, DateTime paidAt)
        {
            var order = new Order(Guid.NewGuid(), Commission.Create(0.25m, total), Guid.NewGuid(), course.CourseID, null);
            order.StudentPaid(paidAt);
            _db.Orders.Add(order);
        }

        private async Task<TeacherSummaryDTO> Run()
        {
            await _db.SaveChangesAsync();
            return await _handler.Handle(new GetTeacherSummaryQuery(), CancellationToken.None);
        }

        [Fact]
        public async Task ATeacherWithoutAnything_GetsZeroesAndSixEmptyMonths()
        {
            var result = await Run();

            result.TotalCourses.Should().Be(0);
            result.ActiveStudents.Should().Be(0);
            result.AverageRating.Should().Be(0);
            result.TotalRevenue.Should().Be(0);
            result.CourseStatusDistribution.Should().Equal(new Dictionary<string, int> { ["InReview"] = 0, ["Published"] = 0, ["Rejected"] = 0 });
            result.MonthlyRevenue.Should().HaveCount(6).And.OnlyContain(m => m.Revenue == 0);
        }

        [Fact]
        public async Task CountsOnlyTheTeachersOwnCourses_AndGroupsThemByStatus()
        {
            AddCourse(_teacherId, "Published one", CourseStatus.Published);
            AddCourse(_teacherId, "Published two", CourseStatus.Published);
            AddCourse(_teacherId, "Waiting", CourseStatus.InReview);
            AddCourse(_teacherId, "Refused", CourseStatus.Rejected);
            AddCourse(Guid.NewGuid(), "Somebody else's", CourseStatus.Published);

            var result = await Run();

            result.TotalCourses.Should().Be(4);
            result.CourseStatusDistribution["Published"].Should().Be(2);
            result.CourseStatusDistribution["InReview"].Should().Be(1);
            result.CourseStatusDistribution["Rejected"].Should().Be(1);
        }

        [Fact]
        public async Task CountsTheEnrollmentsOnTheTeachersCourses()
        {
            var mine = AddCourse(_teacherId, "Mine", CourseStatus.Published);
            var other = AddCourse(Guid.NewGuid(), "Other", CourseStatus.Published);
            _db.Enrollments.Add(new Enrollment(Guid.NewGuid(), Guid.NewGuid(), mine.CourseID, null));
            _db.Enrollments.Add(new Enrollment(Guid.NewGuid(), Guid.NewGuid(), mine.CourseID, null));
            _db.Enrollments.Add(new Enrollment(Guid.NewGuid(), Guid.NewGuid(), other.CourseID, null));

            var result = await Run();

            result.ActiveStudents.Should().Be(2);
        }

        [Fact]
        public async Task CountsAStudentOnceEvenWhenTheyTakeSeveralCoursesOfTheTeacher()
        {
            var first = AddCourse(_teacherId, "First", CourseStatus.Published);
            var second = AddCourse(_teacherId, "Second", CourseStatus.Published);
            var student = Guid.NewGuid();
            _db.Enrollments.Add(new Enrollment(Guid.NewGuid(), student, first.CourseID, null));
            _db.Enrollments.Add(new Enrollment(Guid.NewGuid(), student, second.CourseID, null));

            var result = await Run();

            result.ActiveStudents.Should().Be(1);
        }

        [Fact]
        public async Task AveragesTheRatings_IgnoringDeletedReviewsAndOtherTeachers()
        {
            var mine = AddCourse(_teacherId, "Mine", CourseStatus.Published);
            var other = AddCourse(Guid.NewGuid(), "Other", CourseStatus.Published);
            _db.CourseReviews.Add(new CourseReview(mine.CourseID, Guid.NewGuid(), 5f, null));
            _db.CourseReviews.Add(new CourseReview(mine.CourseID, Guid.NewGuid(), 4f, null));
            var deleted = new CourseReview(mine.CourseID, Guid.NewGuid(), 1f, null);
            deleted.DeleteReview();
            _db.CourseReviews.Add(deleted);
            _db.CourseReviews.Add(new CourseReview(other.CourseID, Guid.NewGuid(), 1f, null));

            var result = await Run();

            result.AverageRating.Should().Be(4.5);
        }

        [Fact]
        public async Task AddsUpTheTeachersShareOfPaidOrders_AndPutsRecentOnesInTheirMonth()
        {
            var mine = AddCourse(_teacherId, "Mine", CourseStatus.Published);
            var other = AddCourse(Guid.NewGuid(), "Other", CourseStatus.Published);

            // 25% goes to the platform: the teacher keeps 75
            AddPaidOrder(mine, 100m, DateTime.UtcNow);
            AddPaidOrder(mine, 200m, DateTime.UtcNow.AddMonths(-2));
            AddPaidOrder(other, 1000m, DateTime.UtcNow);

            // Never paid: not revenue
            _db.Orders.Add(new Order(Guid.NewGuid(), Commission.Create(0.25m, 500m), Guid.NewGuid(), mine.CourseID, null));

            var result = await Run();

            result.TotalRevenue.Should().Be(75m + 150m);
            result.MonthlyRevenue.Should().HaveCount(6);
            result.MonthlyRevenue.Last().Month.Should().Be(DateTime.UtcNow.ToString("yyyy-MM"));
            result.MonthlyRevenue.Last().Revenue.Should().Be(75m);
            result.MonthlyRevenue.Single(m => m.Month == DateTime.UtcNow.AddMonths(-2).ToString("yyyy-MM")).Revenue.Should().Be(150m);
        }

        [Fact]
        public async Task AnOrderOlderThanSixMonths_CountsInTheTotalButNotInTheChart()
        {
            var mine = AddCourse(_teacherId, "Mine", CourseStatus.Published);
            AddPaidOrder(mine, 100m, DateTime.UtcNow.AddMonths(-8));

            var result = await Run();

            result.TotalRevenue.Should().Be(75m);
            result.MonthlyRevenue.Should().OnlyContain(m => m.Revenue == 0);
        }

        [Fact]
        public async Task WithoutSignIn_IsRefused()
        {
            _currentUser.Setup(c => c.Id).Returns((Guid?)null);

            var act = () => _handler.Handle(new GetTeacherSummaryQuery(), CancellationToken.None);

            await act.Should().ThrowAsync<AuthenticateException>();
        }
    }
}
