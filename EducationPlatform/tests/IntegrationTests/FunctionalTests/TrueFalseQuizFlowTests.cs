using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using API.Models.Common;
using Application.Features.Enrollments.Commands;
using Application.Results;
using Domain.AcademicManagement.Aggregate;
using Domain.CourseManagement.Entity;
using Domain.CourseManagement.Enum;
using Domain.EnrollmentManagement.Aggregate;
using Domain.EnrollmentManagement.Entity;
using Domain.IdentityManagement.Aggregate;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntegrationTests.FunctionalTests
{
    /// <summary>A true/false question is created with its value alone, and is graded like any other.</summary>
    public class TrueFalseQuizFlowTests : IntegrationTestBase
    {
        public TrueFalseQuizFlowTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        private async Task<Guid> CreateCourseWithQuizzesAsync()
        {
            await LoginExistingUserAsync("teacher@example.com", "Password123!");

            var (gradeId, subjectId) = await ExecuteDbContextAsync(async db =>
                ((await db.Set<Grade>().FirstAsync()).GradeID, (await db.Set<Subject>().FirstAsync()).SubjectID));

            var fields = new Dictionary<string, string>
            {
                { "Title", "Logic" },
                { "Description", "Truth and falsehood" },
                { "Price", "15000" },
                { "ThumbnailName", "logic.jpg" },
                { "Slug", "logic-1" },
                { "Prerequisites", "none" },
                { "LearningOutcomes", "Reason clearly" },
                { "GradeID", gradeId.ToString() },
                { "SubjectID", subjectId.ToString() },
                { "Chapters[0].Title", "Chapter 1" },
                { "Chapters[0].Description", "Basics" },
                { "Chapters[0].Order", "1" },
                { "Chapters[0].Lessons[0].Title", "Lesson 1" },
                { "Chapters[0].Lessons[0].Objectives", "Know the basics" },
                { "Chapters[0].Lessons[0].Description", "Introduction" },
                { "Chapters[0].Lessons[0].VideoUrl", "https://example.com/logic" },
                { "Chapters[0].Lessons[0].Order", "1" },

                // True/false: only the value, no answers and no options
                { "Chapters[0].Lessons[0].Quizzes[0].Question", "The sky is blue." },
                { "Chapters[0].Lessons[0].Quizzes[0].Note", "Daytime, clear weather" },
                { "Chapters[0].Lessons[0].Quizzes[0].Answer.Type", "3" },
                { "Chapters[0].Lessons[0].Quizzes[0].Answer.TrueOrFalse", "true" },

                // Single choice still needs its answer and its options
                { "Chapters[0].Lessons[0].Quizzes[1].Question", "What is 1 + 1?" },
                { "Chapters[0].Lessons[0].Quizzes[1].Answer.Type", "1" },
                { "Chapters[0].Lessons[0].Quizzes[1].Answer.CorrectAnswers[0]", "2" },
                { "Chapters[0].Lessons[0].Quizzes[1].Answer.Options[0]", "1" },
                { "Chapters[0].Lessons[0].Quizzes[1].Answer.Options[1]", "2" }
            };

            var content = CreateMultipartFormContent(fields, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0 }, "ThumbnailFile", "logic.jpg", "image/jpeg");
            var response = await Client.PostAsync("/api/courses", content);

            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<ApiResponse<Guid>>())!.Data;
        }

        [Fact]
        public async Task ATrueFalseQuiz_CanBeCreatedWithoutAnswersOrOptions()
        {
            var courseId = await CreateCourseWithQuizzesAsync();

            var quizzes = await ExecuteDbContextAsync(db => db.Set<Quiz>()
                .Include(q => q.Answer)
                .Where(q => db.Set<Lesson>().Any(l => l.LessonID == q.LessonID &&
                            db.Set<Chapter>().Any(c => c.ChapterID == l.ChapterID && c.CourseID == courseId)))
                .ToListAsync());

            quizzes.Should().HaveCount(2);
            var trueFalse = quizzes.Single(q => q.Question == "The sky is blue.");
            trueFalse.Answer.Type.Should().Be(QuizType.TrueFalse);
            trueFalse.Answer.CorrectAnswers.Should().Equal("True");
            trueFalse.Answer.Options.Should().BeNullOrEmpty();

            var single = quizzes.Single(q => q.Question == "What is 1 + 1?");
            single.Answer.Type.Should().Be(QuizType.SingleChoice);
            single.Answer.CorrectAnswers.Should().Equal("2");
        }

        [Fact]
        public async Task AStudent_IsGradedOnATrueFalseQuiz()
        {
            var courseId = await CreateCourseWithQuizzesAsync();

            var (enrollmentId, chapterId, lessonId, quizId) = await ExecuteDbContextAsync<(Guid, Guid, Guid, Guid)>(async db =>
            {
                var student = await db.Set<User>().FirstAsync(u => u.Email == "student@example.com");
                var enrollment = new Enrollment(Guid.NewGuid(), student.UserID, courseId, null);
                db.Set<Enrollment>().Add(enrollment);
                db.Set<CourseProgress>().Add(new CourseProgress(Guid.NewGuid(), enrollment.EnrollmentID));

                var quiz = await db.Set<Quiz>().FirstAsync(q => q.Question == "The sky is blue.");
                var lesson = await db.Set<Lesson>().FirstAsync(l => l.LessonID == quiz.LessonID);
                await db.SaveChangesAsync();

                return (enrollment.EnrollmentID, lesson.ChapterID, lesson.LessonID, quiz.QuizID);
            });

            var student = await CreateAuthenticatedClientAsync("student@example.com", "Password123!");

            async Task<bool> Submit(string answer)
            {
                var response = await student.PostAsJsonAsync("/api/enrollments/progress/quiz", new
                {
                    EnrollmentID = enrollmentId,
                    ChapterID = chapterId,
                    LessonID = lessonId,
                    QuizID = quizId,
                    SelectedAnswers = new List<string> { answer }
                });
                response.StatusCode.Should().Be(HttpStatusCode.OK);
                return (await response.Content.ReadFromJsonAsync<ApiResponse<SubmitQuizResult>>())!.Data!.IsCorrect;
            }

            (await Submit("False")).Should().BeFalse();
            (await Submit("true")).Should().BeTrue();
        }
    }
}
