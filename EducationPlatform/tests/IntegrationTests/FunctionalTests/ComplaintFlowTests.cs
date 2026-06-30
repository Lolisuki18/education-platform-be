using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using API.Models.Common;
using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Enum;
using Domain.EnrollmentManagement.Aggregate;
using Domain.IdentityManagement.Aggregate;
using FluentAssertions;
using IntegrationTests;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FunctionalTests
{
    public class ComplaintFlowTests : IntegrationTestBase
    {
        public ComplaintFlowTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        [Fact]
        public async Task SubmitAndResolveComplaintFlow_ShouldSucceed()
        {
            var studentClient = await CreateAuthenticatedClientAsync("student@example.com", "Password123!");
            var adminClient = await CreateAuthenticatedClientAsync("admin@example.com", "Password123!");

            Guid studentId = Guid.Empty;
            Guid courseId = Guid.Empty;
            await ExecuteDbContextAsync(async db =>
            {
                var student = await db.Set<User>().FirstAsync(u => u.Email == "student@example.com");
                studentId = student.UserID;

                var course = await db.Set<Course>().FirstAsync(c => c.Title == "Math algebra");
                courseId = course.CourseID;

                // Enroll the student first to satisfy the complaint policy
                var enrollment = new Enrollment(Guid.NewGuid(), studentId, courseId, null);
                db.Set<Enrollment>().Add(enrollment);
                await db.SaveChangesAsync();
            });

            // 1. Student submits a complaint
            var fields = new Dictionary<string, string>
            {
                { "CourseId", courseId.ToString() },
                { "Reason", "The course content is not updated." }
            };
            var content = CreateMultipartFormContent(fields, new byte[] { 1, 2, 3 }, "EvidenceImage", "evidence.jpg", "image/jpeg");
            var complaintResponse = await studentClient.PostAsync("/api/courses/complaints", content);
            complaintResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Fetch the complaint ID from db
            Guid complaintId = Guid.Empty;
            await ExecuteDbContextAsync(async db =>
            {
                var complaint = await db.Set<Complaint>().FirstOrDefaultAsync(c => c.StudentID == studentId && c.CourseID == courseId);
                complaint.Should().NotBeNull();
                complaint!.Status.Should().Be(ComplaintStatus.Pending);
                complaintId = complaint.ComplaintID;
            });

            // 2. Admin reviews the complaint with empty AdminNote to assert it works/preserves
            var reviewPayloadEmpty = new
            {
                ComplaintID = complaintId,
                IsApproved = true,
                AdminNote = "   " // empty/whitespace note
            };
            var reviewResponse1 = await adminClient.PostAsJsonAsync("/api/courses/complaints/review", reviewPayloadEmpty);
            reviewResponse1.StatusCode.Should().Be(HttpStatusCode.OK);

            // Assert status updated to Approved
            await ExecuteDbContextAsync(async db =>
            {
                var complaint = await db.Set<Complaint>().FindAsync(complaintId);
                complaint!.Status.Should().Be(ComplaintStatus.Approved);
            });
        }
    }
}
