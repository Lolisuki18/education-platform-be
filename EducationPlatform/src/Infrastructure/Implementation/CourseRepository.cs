using Domain.Common.Interfaces;
using Infrastructure.Persistence;
using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Entity;
using Domain.CourseManagement.Enum;
using Domain.IdentityManagement.ValueObject;
using Domain.IdentityManagement.Enum;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Implementation
{
    public class CourseRepository :
        GenericRepository<Course>,
        ICourseRepository
    {
        public CourseRepository(EducationPlatformDBContext context) : base(context) { }

        public async Task<IEnumerable<Course>> GetAllCourses(
            string? title,
            decimal? price,
            string? teacherName,
            string? gradeName,
            string? subjectName,
            int pageIndex,
            int pageSize,
            Guid? teacherId,
            Role? callerRole)
        {
            // Safety guards
            pageIndex = pageIndex < 1 ? 1 : pageIndex;
            pageSize = pageSize <= 0 ? 10 : pageSize;

            IQueryable<Course> query = context.Courses
                .AsNoTracking()
                .Include(c => c.Teacher)
                .Include(c => c.Grade)
                .Include(c => c.Subject);

            // ---------- Filters ----------
            if (!string.IsNullOrWhiteSpace(title))
            {
                query = query.Where(c =>
                    EF.Functions.Like(c.Title, $"%{title}%"));
            }

            if (price.HasValue)
            {
                query = query.Where(c =>
                    c.Price.Amount == price.Value);
            }

            if (!string.IsNullOrWhiteSpace(teacherName))
            {
                query = query.Where(c =>
                    EF.Functions.Like(c.Teacher.Name, $"%{teacherName}%"));
            }
            if (!string.IsNullOrWhiteSpace(gradeName))
            {
                query = query.Where(c =>
                    EF.Functions.Like(c.Grade.Name, $"%{gradeName}%"));
            }

            if (!string.IsNullOrWhiteSpace(subjectName))
            {
                query = query.Where(c =>
                    EF.Functions.Like(c.Subject.Name, $"%{subjectName}%"));
            }

            if (teacherId.HasValue)
            {
                query = query.Where(c =>
                    c.TeacherID == teacherId);
            }

            if (callerRole == Role.Student || callerRole == null)
            {
                query = query.Where(c =>
                    c.Status == CourseStatus.Published);
            }

            // ---------- Sorting (IMPORTANT for paging) ----------
            query = query.OrderByDescending(c => c.CreatedAt);

            // ---------- Paging ----------
            query = query
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize);

            return await query.ToListAsync();
        }


        public async Task<bool> HasPublishedCourseAsync(
            Guid teacherId,
            CancellationToken cancellationToken = default)
        {
            return await context.Courses
                .AsNoTracking()
                .AnyAsync(c => c.TeacherID == teacherId && c.Status == CourseStatus.Published, cancellationToken);
        }

        public async Task<bool> SlugExistsAsync(
            string slug,
            CancellationToken cancellationToken = default)
        {
            return await context.Courses
                .AsNoTracking()
                .AnyAsync(c => c.Slug == slug, cancellationToken);
        }

        public async Task<Course?> GetCourseMetadataByID(Guid courseId)
        {
            return await context.Courses
                .AsNoTracking()
                .Include(c => c.Teacher)
                .Include(c => c.Grade)
                .Include(c => c.Subject)
                .Include(c => c.ViolatedPolicies)
                    .ThenInclude(vp => vp.Policy)
                        .ThenInclude(p => p.PolicyRules)
                .FirstOrDefaultAsync(c => c.CourseID == courseId);
        }

        public async Task<Course?> GetCourseDetailByID(Guid courseId)
        {
            return await context.Courses
                .AsNoTracking()
                .AsSplitQuery()
                .Include(c => c.Teacher)
                .Include(c => c.Grade)
                .Include(c => c.Subject)
                .Include(c => c.ViolatedPolicies)
                    .ThenInclude(vp => vp.Policy)
                        .ThenInclude(p => p.PolicyRules)
                .Include(c => c.Chapters)
                    .ThenInclude(ch => ch.Lessons)
                        .ThenInclude(l => l.Quizzes)
                .Include(c => c.Chapters)
                    .ThenInclude(ch => ch.Lessons)
                        .ThenInclude(l => l.Assignments)
                .Include(c => c.Chapters)
                    .ThenInclude(ch => ch.Lessons)
                        .ThenInclude(l => l.Materials)
                .FirstOrDefaultAsync(c => c.CourseID == courseId);
        }


        public async Task ReplaceViolatedPolicies(
            Guid courseId,
            IEnumerable<ViolatedPolicy> newViolatedPolicies)
        {
            if (newViolatedPolicies == null)
                newViolatedPolicies = Enumerable.Empty<ViolatedPolicy>();

            // Remove existing policies for the course
            var existingPolicies = await context.ViolatedPolicies
                                          .Where(vp => vp.CourseID == courseId)
                                          .ToListAsync();

            if (existingPolicies.Any())
                context.ViolatedPolicies.RemoveRange(existingPolicies);

            // Add new policies
            if (newViolatedPolicies.Any())
                context.ViolatedPolicies.AddRange(newViolatedPolicies);
        }

        public void AddChapters(IEnumerable<Chapter> chapters)
        {
            if (chapters == null || !chapters.Any())
                return;

            context.Chapters.AddRange(chapters);
        }

        public void AddLessons(
            IEnumerable<Lesson> lessons)
        {
            if (lessons == null || !lessons.Any())
                return;

            context.Lessons.AddRange(lessons);
        }

        public void AddQuizzes(
            IEnumerable<Quiz> quizzes)
        {
            if (quizzes == null || !quizzes.Any())
                return;

            context.Quizzes.AddRange(quizzes);
        }

        public void AddAssignments(IEnumerable<Assignment> assignments)
        {
            if (assignments == null || !assignments.Any())
                return;

            context.Assignments.AddRange(assignments);
        }

        public void AddMaterials(
            IEnumerable<Material> materials)
        {
            if (materials == null || !materials.Any())
                return;

            context.Materials.AddRange(materials);
        }


        public async Task<(
            int InReview,
            int Rejected,
            int Published,
            Dictionary<string, int> GradeCounts,
            Dictionary<string, int> SubjectCounts
        )> Summary(DateTime? from, DateTime? to)
        {
            var query = context.Courses.AsNoTracking().AsQueryable();

            if (from.HasValue)
                query = query.Where(c => c.CreatedAt >= from.Value);

            if (to.HasValue)
                query = query.Where(c => c.CreatedAt <= to.Value);

            // ===== 1. Status summary =====
            var status = await query
                .GroupBy(c => 1)
                .Select(g => new
                {
                    InReview = g.Count(c => c.Status == CourseStatus.InReview),
                    Rejected = g.Count(c => c.Status == CourseStatus.Rejected),
                    Published = g.Count(c => c.Status == CourseStatus.Published)
                })
                .FirstOrDefaultAsync();

            // ===== 2. Grade distribution =====
            var gradeDict = await query
                .GroupBy(c => c.Grade.Name)
                .Select(g => new
                {
                    Grade = g.Key,
                    Count = g.Count()
                })
                .ToDictionaryAsync(x => x.Grade, x => x.Count);

            // ===== 3. Subject distribution =====
            var subjectDict = await query
                .GroupBy(c => c.Subject.Name)
                .Select(g => new
                {
                    Subject = g.Key,
                    Count = g.Count()
                })
                .ToDictionaryAsync(x => x.Subject, x => x.Count);

            return (
                status?.InReview ?? 0,
                status?.Rejected ?? 0,
                status?.Published ?? 0,
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
            var query = context.Courses.AsNoTracking().AsQueryable();

            // ===== Filters =====
            if (from.HasValue)
                query = query.Where(c => c.CreatedAt >= from.Value);

            if (to.HasValue)
                query = query.Where(c => c.CreatedAt <= to.Value);

            if (gradeId.HasValue)
                query = query.Where(c => c.GradeID == gradeId);

            if (subjectId.HasValue)
                query = query.Where(c => c.SubjectID == subjectId);

            // ===== Step 1: Group in DB (no string formatting) =====
            var rawData = await query
                .GroupBy(c => new
                {
                    c.CreatedAt.Year,
                    c.CreatedAt.Month,
                    c.CreatedAt.Day
                })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    g.Key.Day,
                    Count = g.Count()
                })
                .ToListAsync();

            // ===== Step 2: Format labels in memory =====
            var data = rawData
                .Select(x => new
                {
                    Label = groupBy.ToLower() switch
                    {
                        "day" => $"{x.Year}-{x.Month:D2}-{x.Day:D2}",
                        "month" => $"{x.Year}-{x.Month:D2}",
                        "year" => x.Year.ToString(),
                        _ => throw new ArgumentException("Invalid groupBy")
                    },
                    x.Count
                })
                .OrderBy(x => x.Label)
                .ToList();

            return new Dictionary<string, List<(string, decimal)>>
            {
                {
                    "Courses",
                    data.Select(x => (x.Label, (decimal)x.Count)).ToList()
                }
            };
        }
    }
}
