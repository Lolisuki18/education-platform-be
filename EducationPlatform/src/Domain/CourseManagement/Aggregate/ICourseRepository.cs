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
            Role? callerRole,
            CancellationToken cancellationToken = default);

        /// <summary>Whether a course already uses this slug (slugs are unique).</summary>
        Task<bool> SlugExistsAsync(
            string slug,
            CancellationToken cancellationToken = default);

        /// <summary>Whether the teacher still has a course that students can buy.</summary>
        Task<bool> HasPublishedCourseAsync(
            Guid teacherId,
            CancellationToken cancellationToken = default);

        Task<Course?> GetCourseMetadataByID(
            Guid courseId,
            CancellationToken cancellationToken = default);

        Task<Course?> GetCourseDetailByID(
            Guid courseId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// The course with its chapters, <b>tracked</b>: what an admin review changes (the course, the chapter notes)
        /// is saved with the unit of work and the domain event the review raises is dispatched. The read models above
        /// are no-tracking copies, so changes made to them are silently lost.
        /// </summary>
        Task<Course?> GetCourseForReview(
            Guid courseId,
            CancellationToken cancellationToken = default);


        Task ReplaceViolatedPolicies(
            Guid courseId,
            IEnumerable<ViolatedPolicy> newViolatedPolicies,
            CancellationToken cancellationToken = default);

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
            DateTime? to,
            CancellationToken cancellationToken = default);

        Task<Dictionary<string, List<(string Label, decimal Value)>>> AnalyticsGrowth(
            DateTime? from,
            DateTime? to,
            string groupBy,
            Guid? gradeId,
            Guid? subjectId,
            CancellationToken cancellationToken = default);
    }
}
