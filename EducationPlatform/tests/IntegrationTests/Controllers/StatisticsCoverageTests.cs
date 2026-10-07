using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Domain.AcademicManagement.Aggregate;
using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Entity;
using Domain.EnrollmentManagement.Aggregate;
using Domain.IdentityManagement.Aggregate;
using Domain.OrderManagement.Aggregate;
using Domain.OrderManagement.ValueObject;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntegrationTests.Controllers
{
    /// <summary>
    /// The report queries are translated to SQL at run time, so a variant nobody ever called can fail only in production.
    /// Every parameter combination is run here against real data.
    /// </summary>
    public class StatisticsCoverageTests : IntegrationTestBase
    {
        public StatisticsCoverageTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        private async Task<(Guid Grade, Guid Subject)> SeedActivityAsync()
        {
            return await ExecuteDbContextAsync(async db =>
            {
                var student = await db.Set<User>().FirstAsync(u => u.Email == "student@example.com");
                var course = await db.Set<Course>().FirstAsync(c => c.Title == "Math algebra");

                // Paying enrolls the student (the order's domain event does it)
                var order = new Order(Guid.NewGuid(), Commission.Create(0.15m, 50000m), student.UserID, course.CourseID, null);
                order.StudentPaid(null);
                db.Orders.Add(order);

                db.CourseReviews.Add(new CourseReview(course.CourseID, student.UserID, 4.5f, "Good"));
                await db.SaveChangesAsync();

                var enrollment = await db.Set<Enrollment>().SingleAsync(e => e.StudentID == student.UserID && e.CourseID == course.CourseID);
                enrollment.CompleteEnrollment(null);
                await db.SaveChangesAsync();

                return (course.GradeID, course.SubjectID);
            });
        }

        private async Task AssertOkAsync(HttpClient client, string url)
        {
            var response = await client.GetAsync(url);
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"{url} -> {await response.Content.ReadAsStringAsync()}");
        }

        [Fact]
        public async Task EveryGrowthReport_RunsForEveryTypeAndGrouping()
        {
            var (grade, subject) = await SeedActivityAsync();
            var admin = await CreateAuthenticatedClientAsync("admin@example.com", "Password123!");

            foreach (var type in new[] { "User", "Course", "Enrollment", "Revenue" })
                foreach (var groupBy in new[] { "Day", "Month", "Year" })
                {
                    var common = $"Type={type}&GroupBy={groupBy}&From=2020-01-01&To=2030-01-01";
                    await AssertOkAsync(admin, $"/api/statistics/analytics/growth?{common}");
                    await AssertOkAsync(admin, $"/api/statistics/analytics/normalized-growth?{common}");
                }

            foreach (var revenue in new[] { "All", "Commission", "Teacher" })
            {
                await AssertOkAsync(admin, $"/api/statistics/analytics/growth?Type=Revenue&GroupBy=Month&RevenueType={revenue}");
                await AssertOkAsync(admin, $"/api/statistics/analytics/normalized-growth?Type=Revenue&GroupBy=Month&RevenueType={revenue}");
            }

            await AssertOkAsync(admin, "/api/statistics/analytics/growth?Type=User&GroupBy=Month&UserRole=Student");
            await AssertOkAsync(admin, $"/api/statistics/analytics/growth?Type=Course&GroupBy=Month&CourseGradeId={grade}&CourseSubjectId={subject}");
            await AssertOkAsync(admin, $"/api/statistics/analytics/growth?Type=Enrollment&GroupBy=Month&EnrollmentGradeId={grade}&EnrollmentSubjectId={subject}");
            await AssertOkAsync(admin, "/api/statistics/analytics/growth?Type=Revenue&GroupBy=Day&ComparisonRanges[0].Label=Last&ComparisonRanges[0].From=2020-01-01&ComparisonRanges[0].To=2021-01-01");
        }

        [Fact]
        public async Task TheDemandAndTopReports_RunWithEveryFilter()
        {
            var (grade, subject) = await SeedActivityAsync();
            var admin = await CreateAuthenticatedClientAsync("admin@example.com", "Password123!");

            foreach (var groupBy in new[] { "Day", "Month", "Year" })
            {
                await AssertOkAsync(admin, $"/api/statistics/analytics/demand-supply?GroupBy={groupBy}");
                await AssertOkAsync(admin, $"/api/statistics/analytics/demand-supply?GroupBy={groupBy}&CourseGradeId={grade}&CourseSubjectId={subject}&EnrollmentGradeId={grade}&EnrollmentSubjectId={subject}");
            }

            await AssertOkAsync(admin, "/api/statistics/analytics/top-performance");
            await AssertOkAsync(admin, "/api/statistics/analytics/top-performance?Top=3");
            await AssertOkAsync(admin, $"/api/statistics/analytics/top-performance?GradeId={grade}");
            await AssertOkAsync(admin, $"/api/statistics/analytics/top-performance?SubjectId={subject}");
            await AssertOkAsync(admin, $"/api/statistics/analytics/top-performance?GradeId={grade}&SubjectId={subject}&From=2020-01-01&To=2030-01-01");
            await AssertOkAsync(admin, "/api/statistics/summary?From=2020-01-01&To=2030-01-01");
        }

        [Fact]
        public async Task TheTeacherSummary_AddsUpTheTeachersWork()
        {
            await SeedActivityAsync();
            var teacher = await CreateAuthenticatedClientAsync("teacher@example.com", "Password123!");

            var response = await teacher.GetAsync("/api/statistics/teacher/summary");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStringAsync();
            using var json = System.Text.Json.JsonDocument.Parse(body);
            var data = json.RootElement.GetProperty("data");
            data.GetProperty("totalCourses").GetInt32().Should().Be(2);
            data.GetProperty("activeStudents").GetInt32().Should().Be(1);
            data.GetProperty("averageRating").GetDouble().Should().Be(4.5);
            data.GetProperty("totalRevenue").GetDecimal().Should().Be(42500m);
        }
    }
}
