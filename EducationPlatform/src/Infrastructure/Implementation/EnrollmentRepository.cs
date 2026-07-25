using System.Text;
using Domain.Common.Interfaces;
using Infrastructure.Persistence;
using Domain.EnrollmentManagement.Aggregate;
using Domain.EnrollmentManagement.Entity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Implementation
{
    public class EnrollmentRepository :
        GenericRepository<Enrollment>,
        IEnrollmentRepository
    {
        #region Attributes
        #endregion

        #region Properties
        #endregion

        public EnrollmentRepository(EducationPlatformDBContext context) : base(context) { }

        #region Methods

        public async Task<IEnumerable<Enrollment>> GetStudentEnrollments(Guid studentId, int pageIndex = 1, int pageSize = 10, CancellationToken cancellationToken = default)
        {
            return await context.Enrollments
                .AsNoTracking()
                .Include(e => e.Course)
                .Include(e => e.CourseProgress)
                .Where(e => e.StudentID == studentId)
                .OrderByDescending(e => e.EnrolledAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
        }

        public async Task<Enrollment?> GetEnrollmentDetailByID(Guid enrollmentId)
        {
            return await context.Enrollments
                .AsNoTracking()
                .AsSplitQuery()
                .Include(e => e.Course)
                    .ThenInclude(c => c.Teacher)
                .Include(e => e.Course)
                    .ThenInclude(c => c.Grade)
                .Include(e => e.Course)
                    .ThenInclude(c => c.Subject)
                .Include(e => e.Course)
                    .ThenInclude(c => c.ViolatedPolicies)
                        .ThenInclude(vp => vp.Policy)
                            .ThenInclude(p => p.PolicyRules)
                .Include(e => e.Course)
                    .ThenInclude(c => c.Chapters)
                        .ThenInclude(ch => ch.Lessons)
                            .ThenInclude(l => l.Quizzes)
                                .ThenInclude(q => q.Answer)
                .Include(e => e.CourseProgress)
                    .ThenInclude(cp => cp.ChapterProgresses)
                        .ThenInclude(chp => chp.LessonProgresses)
                            .ThenInclude(lp => lp.QuizProgresses)
                                .ThenInclude(qp => qp.Quiz)
                .Include(e => e.CourseProgress)
                    .ThenInclude(cp => cp.ChapterProgresses)
                        .ThenInclude(chp => chp.LessonProgresses)
                            .ThenInclude(lp => lp.Lesson)
                .FirstOrDefaultAsync(e => e.EnrollmentID == enrollmentId);
        }

        public async Task<Enrollment?> GetEnrollmentForUpdate(Guid enrollmentId)
        {
            return await context.Enrollments
                .AsSplitQuery()
                .Include(e => e.CourseProgress)
                    .ThenInclude(cp => cp.ChapterProgresses)
                        .ThenInclude(chp => chp.LessonProgresses)
                            .ThenInclude(lp => lp.QuizProgresses)
                .Include(e => e.CourseProgress)
                    .ThenInclude(cp => cp.ChapterProgresses)
                        .ThenInclude(chp => chp.LessonProgresses)
                            .ThenInclude(lp => lp.Lesson)
                .FirstOrDefaultAsync(e => e.EnrollmentID == enrollmentId);
        }

        public async Task<List<Guid>> GetEnrolledStudentIdsByCourseId(Guid courseId)
        {
            return await context.Enrollments
                .Where(e => e.CourseID == courseId)
                .Select(e => e.StudentID)
                .Distinct()
                .ToListAsync();
        }

        public async Task UpsertLessonProgress(Guid enrollmentId, Guid chapterId, Guid lessonId, bool isCompleted)
        {
            // Load CourseProgress with ChapterProgresses
            var cp = await context.CourseProgresses
                .Include(x => x.ChapterProgresses)
                    .ThenInclude(chp => chp.LessonProgresses)
                .FirstOrDefaultAsync(x => x.EnrollmentID == enrollmentId);

            if (cp == null)
            {
                var enrollment = await context.Enrollments.FirstOrDefaultAsync(e => e.EnrollmentID == enrollmentId);
                if (enrollment == null) throw new InvalidOperationException("Enrollment not found");

                cp = new CourseProgress(Guid.NewGuid(), enrollmentId);
                context.CourseProgresses.Add(cp);
            }

            // Load or create ChapterProgress
            var chp = cp.ChapterProgresses.FirstOrDefault(x => x.ChapterID == chapterId);
            if (chp == null)
            {
                chp = cp.AddChapterProgress(chapterId);
                context.ChapterProgresses.Add(chp);
            }

            // Load or create LessonProgress
            var lp = chp.LessonProgresses.FirstOrDefault(x => x.LessonID == lessonId);
            if (lp == null)
            {
                lp = chp.AddLessonProgress(lessonId);
                context.LessonProgresses.Add(lp);
            }

            // Mark as completed if needed
            if (isCompleted && !lp.IsCompleted)
                lp.MarkCompleted();

            // Recalculate all progress
            chp.RecalculateCompletion();
            cp.RecalculateCompletion();

            // Update Enrollment CompletedAt if course finished
            if (cp.IsCompleted)
            {
                var enrollment = await context.Enrollments.FirstOrDefaultAsync(e => e.EnrollmentID == enrollmentId);
                if (enrollment != null && enrollment.CompletedAt == null)
                    enrollment.CompleteEnrollment(null);
            }
        }

        public async Task<(bool isCorrect, List<string> correctAnswers, string explanation)> UpsertQuizProgress(
            Guid enrollmentId,
            Guid chapterId,
            Guid lessonId,
            Guid quizId,
            List<string> submittedAnswers)
        {
            // Load CourseProgress
            var cp = await context.CourseProgresses
                .Include(x => x.ChapterProgresses)
                    .ThenInclude(chp => chp.LessonProgresses)
                        .ThenInclude(lp => lp.QuizProgresses)
                .FirstOrDefaultAsync(x => x.EnrollmentID == enrollmentId);

            if (cp == null)
            {
                var enrollment = await context.Enrollments.FirstOrDefaultAsync(e => e.EnrollmentID == enrollmentId);
                if (enrollment == null)
                    throw new InvalidOperationException("Enrollment not found");

                cp = new CourseProgress(Guid.NewGuid(), enrollmentId);
                context.CourseProgresses.Add(cp);
            }

            // ChapterProgress
            var chp = cp.ChapterProgresses.FirstOrDefault(x => x.ChapterID == chapterId);
            if (chp == null)
            {
                chp = cp.AddChapterProgress(chapterId);
                context.ChapterProgresses.Add(chp);
            }

            // LessonProgress
            var lp = chp.LessonProgresses.FirstOrDefault(x => x.LessonID == lessonId);
            if (lp == null)
            {
                lp = chp.AddLessonProgress(lessonId);
                context.LessonProgresses.Add(lp);
            }

            // Load Quiz with Answer
            var quiz = await context.Quizzes
                .Include(q => q.Answer)
                .FirstOrDefaultAsync(q => q.QuizID == quizId);

            if (quiz == null)
                throw new InvalidOperationException("Quiz not found");

            // Normalize submitted answers
            var submitted = submittedAnswers?
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(NormalizeAnswer)
                .ToList() ?? new List<string>();

            // Normalize correct answers
            var correct = quiz.Answer.CorrectAnswers
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Select(NormalizeAnswer)
                .ToList();

            bool isCorrect;

            if (quiz.Answer.Type == Domain.CourseManagement.Enum.QuizType.SingleChoice ||
                quiz.Answer.Type == Domain.CourseManagement.Enum.QuizType.TrueFalse)
            {
                isCorrect = submitted.Count == 1 && correct.Contains(submitted.First());
            }
            else
            {
                isCorrect =
                    submitted.Count > 0 &&
                    submitted.Count == correct.Count &&
                    submitted.All(correct.Contains);
            }

            // QuizProgress
            var qp = lp.QuizProgresses.FirstOrDefault(x => x.QuizID == quizId);
            if (qp == null)
            {
                qp = lp.AddQuizProgress(quizId);
                context.QuizProgresses.Add(qp);
            }

            qp.RegisterAttempt(isCorrect);

            // Recalculate completion
            lp.RecalculateCompletion();
            chp.RecalculateCompletion();
            cp.RecalculateCompletion();

            // Mark enrollment completed
            if (cp.IsCompleted)
            {
                var enrollment = await context.Enrollments.FirstOrDefaultAsync(e => e.EnrollmentID == enrollmentId);
                if (enrollment != null && enrollment.CompletedAt == null)
                {
                    enrollment.GetType()
                        .GetProperty("CompletedAt")?
                        .SetValue(enrollment, DateTime.UtcNow);
                }
            }

            return (isCorrect, correct, quiz.Note ?? "No explanation provided.");
        }

        public async Task<Enrollment?> GetEnrollmentStatistic(Guid enrollmentId)
        {
            return await context.Enrollments
                .AsNoTracking()
                .AsSplitQuery()
                .Include(e => e.Course)
                .Include(e => e.CourseProgress)
                    .ThenInclude(cp => cp.ChapterProgresses)
                        .ThenInclude(chp => chp.LessonProgresses)
                            .ThenInclude(lp => lp.QuizProgresses)
                .Include(e => e.CourseProgress)
                    .ThenInclude(cp => cp.ChapterProgresses)
                        .ThenInclude(chp => chp.LessonProgresses)
                            .ThenInclude(lp => lp.Lesson)
                .FirstOrDefaultAsync(e => e.EnrollmentID == enrollmentId);
        }

        private static string NormalizeAnswer(string value)
        {
            return value
                .Replace("[", "")
                .Replace("]", "")
                .Replace("\"", "")
                .Trim()
                .Normalize(NormalizationForm.FormC)
                .ToLowerInvariant();
        }

        public async Task<(
           int Total,
           int Completed,
           Dictionary<string, int> GradeCounts,
           Dictionary<string, int> SubjectCounts
       )> Summary(DateTime? from, DateTime? to)
        {
            // ===== Base query with filters =====
            var query = context.Enrollments.AsQueryable();

            if (from.HasValue)
                query = query.Where(e => e.EnrolledAt >= from.Value);

            if (to.HasValue)
                query = query.Where(e => e.EnrolledAt <= to.Value);

            // ===== 1. Basic summary =====
            var summary = await query
                .GroupBy(e => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Completed = g.Count(e => e.CompletedAt != null)
                })
                .FirstOrDefaultAsync();

            // ===== 2. Grade distribution =====
            var gradeDict = await query
                .GroupBy(e => e.Course.Grade.Name)
                .Select(g => new
                {
                    Grade = g.Key,
                    Count = g.Count()
                })
                .ToDictionaryAsync(x => x.Grade, x => x.Count);

            // ===== 3. Subject distribution =====
            var subjectDict = await query
                .GroupBy(e => e.Course.Subject.Name)
                .Select(g => new
                {
                    Subject = g.Key,
                    Count = g.Count()
                })
                .ToDictionaryAsync(x => x.Subject, x => x.Count);

            // ===== Return =====
            return (
                summary?.Total ?? 0,
                summary?.Completed ?? 0,
                gradeDict,
                subjectDict
            );
        }

        public async Task<Dictionary<string, List<(string Label, decimal Value)>>> AnalyticsGrowth(
            DateTime? from,
            DateTime? to,
            string groupBy,
            Guid? gradeId,
            Guid? subjectId)
        {
            var query = context.Enrollments.AsQueryable();

            // ===== Filters =====
            if (from.HasValue)
                query = query.Where(e => e.EnrolledAt >= from.Value);

            if (to.HasValue)
                query = query.Where(e => e.EnrolledAt <= to.Value);

            if (gradeId.HasValue)
                query = query.Where(e => e.Course.GradeID == gradeId);

            if (subjectId.HasValue)
                query = query.Where(e => e.Course.SubjectID == subjectId);

            // Normalize groupBy
            var gb = (groupBy ?? "month").ToLower();

            // ===== Step 1: Dynamic grouping in DB =====
            var rawData = gb switch
            {
                "day" => await query
                    .GroupBy(e => new
                    {
                        e.EnrolledAt.Year,
                        e.EnrolledAt.Month,
                        e.EnrolledAt.Day
                    })
                    .Select(g => new
                    {
                        g.Key.Year,
                        g.Key.Month,
                        g.Key.Day,
                        Count = g.Count()
                    })
                    .ToListAsync(),

                "month" => await query
                    .GroupBy(e => new
                    {
                        e.EnrolledAt.Year,
                        e.EnrolledAt.Month
                    })
                    .Select(g => new
                    {
                        g.Key.Year,
                        g.Key.Month,
                        Day = 0,
                        Count = g.Count()
                    })
                    .ToListAsync(),

                "year" => await query
                    .GroupBy(e => new
                    {
                        e.EnrolledAt.Year
                    })
                    .Select(g => new
                    {
                        g.Key.Year,
                        Month = 0,
                        Day = 0,
                        Count = g.Count()
                    })
                    .ToListAsync(),

                _ => throw new ArgumentException("Invalid groupBy")
            };

            // ===== Step 2: Format labels in memory =====
            var data = rawData
                .Select(x => new
                {
                    Label = gb switch
                    {
                        "day" => $"{x.Year}-{x.Month:D2}-{x.Day:D2}",
                        "month" => $"{x.Year}-{x.Month:D2}",
                        "year" => x.Year.ToString(),
                        _ => $"{x.Year}-{x.Month:D2}"
                    },
                    x.Count,
                    x.Year,
                    x.Month,
                    x.Day
                })
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Month)
                .ThenBy(x => x.Day)
                .ToList();

            // ===== Build result =====
            return new Dictionary<string, List<(string, decimal)>>
            {
                {
                    "Enrollments",
                    data.Select(x => (x.Label, (decimal)x.Count)).ToList()
                }
            };
        }

        public async Task<List<(Guid CourseId, string CourseName, decimal EnrollmentCount)>>
    GetTopCoursesByEnrollment(
        DateTime? from,
        DateTime? to,
        Guid? gradeId,
        Guid? subjectId,
        int top)
        {
            var q = context.Enrollments
                .Include(e => e.Course)
                .AsQueryable();

            if (from.HasValue)
                q = q.Where(e => e.EnrolledAt >= from.Value);

            if (to.HasValue)
                q = q.Where(e => e.EnrolledAt <= to.Value);

            if (gradeId.HasValue)
                q = q.Where(e => e.Course.GradeID == gradeId.Value);

            if (subjectId.HasValue)
                q = q.Where(e => e.Course.SubjectID == subjectId.Value);

            var result = await q
                .GroupBy(e => new { e.CourseID, e.Course.Title })
                .Select(g => new
                {
                    g.Key.CourseID,
                    CourseName = g.Key.Title,
                    EnrollmentCount = g.Count()
                })
                .OrderByDescending(x => x.EnrollmentCount)
                .Take(top)
                .ToListAsync();

            return result
                .Select(x => (x.CourseID, x.CourseName, (decimal)x.EnrollmentCount))
                .ToList();
        }

        public async Task<List<(Guid SubjectId, string SubjectName, decimal EnrollmentCount)>>
            GetTopSubjectsByEnrollment(
                DateTime? from,
                DateTime? to,
                Guid? gradeId,
                int top)
        {
            var query = context.Enrollments
                .Include(e => e.Course)
                .ThenInclude(c => c.Subject)
                .AsQueryable();

            if (from.HasValue)
                query = query.Where(e => e.EnrolledAt >= from.Value);

            if (to.HasValue)
                query = query.Where(e => e.EnrolledAt <= to.Value);

            if (gradeId.HasValue)
                query = query.Where(e => e.Course.GradeID == gradeId.Value);

            var result = await query
                .GroupBy(e => new
                {
                    e.Course.SubjectID,
                    SubjectName = e.Course.Subject.Name
                })
                .Select(g => new
                {
                    g.Key.SubjectID,
                    g.Key.SubjectName,
                    EnrollmentCount = g.Count()
                })
                .OrderByDescending(x => x.EnrollmentCount)
                .Take(top)
                .ToListAsync();

            return result
                .Select(x => (x.SubjectID, x.SubjectName, (decimal)x.EnrollmentCount))
                .ToList();
        }

        public async Task<List<(Guid GradeId, string GradeName, decimal EnrollmentCount)>>
            GetTopGradesByEnrollment(
                DateTime? from,
                DateTime? to,
                Guid? subjectId,
                int top)
        {
            var query = context.Enrollments
                .Include(e => e.Course)
                .ThenInclude(c => c.Grade)
                .AsQueryable();

            if (from.HasValue)
                query = query.Where(e => e.EnrolledAt >= from.Value);

            if (to.HasValue)
                query = query.Where(e => e.EnrolledAt <= to.Value);

            if (subjectId.HasValue)
                query = query.Where(e => e.Course.SubjectID == subjectId.Value);

            var result = await query
                .GroupBy(e => new
                {
                    e.Course.GradeID,
                    GradeName = e.Course.Grade.Name
                })
                .Select(g => new
                {
                    g.Key.GradeID,
                    g.Key.GradeName,
                    EnrollmentCount = g.Count()
                })
                .OrderByDescending(x => x.EnrollmentCount)
                .Take(top)
                .ToListAsync();

            return result
                .Select(x => (x.GradeID, x.GradeName, (decimal)x.EnrollmentCount))
                .ToList();
        }
        #endregion
    }
}

