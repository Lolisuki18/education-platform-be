using Domain.Common.Interfaces;

namespace Domain.EnrollmentManagement.Aggregate
{
    public interface IEnrollmentRepository :
        IGenericRepository<Enrollment>
    {
        // Get all enrollments for a student
        Task<IEnumerable<Enrollment>> GetStudentEnrollments(Guid studentId, int pageIndex = 1, int pageSize = 10, CancellationToken cancellationToken = default);

        // Cheap existence check, not limited by paging
        Task<bool> IsStudentEnrolled(Guid studentId, Guid courseId, CancellationToken cancellationToken = default);

        // Get detailed enrollment with course, chapters, lessons, progress, quizzes
        Task<Enrollment?> GetEnrollmentDetailByID(Guid enrollmentId, CancellationToken cancellationToken = default);

        Task<List<Guid>> GetEnrolledStudentIdsByCourseId(
            Guid courseId,
            CancellationToken cancellationToken = default);

        // Get student statistics across enrollments
        Task<Enrollment?> GetEnrollmentStatistic(Guid enrollmentId, CancellationToken cancellationToken = default);

        // Get enrollment for update (tracked)
        Task<Enrollment?> GetEnrollmentForUpdate(Guid enrollmentId, CancellationToken cancellationToken = default);

        // Upsert lesson progress (handles chapters internally)
        Task UpsertLessonProgress(Guid enrollmentId, Guid chapterId, Guid lessonId, bool isCompleted, CancellationToken cancellationToken = default);

        // Upsert quiz progress (handles chapters/lessons internally)
        Task<(bool isCorrect, List<string> correctAnswers, string explanation)> UpsertQuizProgress(
            Guid enrollmentId,
            Guid chapterId,
            Guid lessonId,
            Guid quizId,
            List<string> submittedAnswers,
            CancellationToken cancellationToken = default);

        Task<(
            int Total,
            int Completed,
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

        Task<List<(Guid CourseId, string CourseName, decimal EnrollmentCount)>>
        GetTopCoursesByEnrollment(
            DateTime? from,
            DateTime? to,
            Guid? gradeId,
            Guid? subjectId,
            int top,
            CancellationToken cancellationToken = default);

        Task<List<(Guid SubjectId, string SubjectName, decimal EnrollmentCount)>>
        GetTopSubjectsByEnrollment(
            DateTime? from,
            DateTime? to,
            Guid? gradeId,
            int top,
            CancellationToken cancellationToken = default);

        Task<List<(Guid GradeId, string GradeName, decimal EnrollmentCount)>>
        GetTopGradesByEnrollment(
            DateTime? from,
            DateTime? to,
            Guid? subjectId,
            int top,
            CancellationToken cancellationToken = default);
    }
}
