using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Entity;
using Domain.CourseManagement.Enum;
using Domain.IdentityManagement.ValueObject;
using Domain.IdentityManagement.Enum;
using Domain.Common.Interfaces;

namespace Domain.CourseManagement.Aggregate
{
    public interface ICourseRepository : IGenericRepository<Course>
    {
        Task<IEnumerable<Course>> GetAllCourses(
            string? title,
            decimal? price,
            string? teacherName,
            string? gradeName,
            string? subjectName,
            int pageIndex,
            int pageSize,
            Guid? teacherId,
            Role? callerRole);

        /// <summary>Whether a course already uses this slug (slugs are unique).</summary>
        Task<bool> SlugExistsAsync(
            string slug,
            CancellationToken cancellationToken = default);

        Task<Course?> GetCourseMetadataByID(
            Guid courseId);

        Task<Course?> GetCourseDetailByID(
            Guid courseId);


        Task ReplaceViolatedPolicies(
            Guid courseId,
            IEnumerable<ViolatedPolicy> newViolatedPolicies);

        void AddChapters(
            IEnumerable<Chapter> chapters);

        void AddLessons(
            IEnumerable<Lesson> lessons);

        void AddQuizzes(
            IEnumerable<Quiz> quizzes);

        void AddAssignments(
            IEnumerable<Assignment> assignments);

        void AddMaterials(
            IEnumerable<Material> materials);


        Task<(
            int InReview,
            int Rejected,
            int Published,
            Dictionary<string, int> GradeCounts,
            Dictionary<string, int> SubjectCounts
        )> Summary(
            DateTime? from,
            DateTime? to);

        Task<Dictionary<string, List<(string Label, decimal Value)>>> AnalyticsGrowth(
            DateTime? from,
            DateTime? to,
            string groupBy,
            Guid? gradeId,
            Guid? subjectId);
    }
}
