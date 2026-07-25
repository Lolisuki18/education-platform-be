using Domain.Common.Interfaces;

namespace Domain.EnrollmentManagement.Aggregate
{
    public interface IEnrollmentRepository :
        IGenericRepository<Enrollment>
    {
        // Get all enrollments for a student
        Task<IEnumerable<Enrollment>> GetStudentEnrollments(Guid studentId, int pageIndex = 1, int pageSize = 10, CancellationToken cancellationToken = default);

        // Get detailed enrollment with course, chapters, lessons, progress, quizzes
        Task<Enrollment?> GetEnrollmentDetailByID(Guid enrollmentId);

        Task<List<Guid>> GetEnrolledStudentIdsByCourseId(
            Guid courseId);

        // Get student statistics across enrollments
        Task<Enrollment?> GetEnrollmentStatistic(Guid enrollmentId);

        // Get enrollment for update (tracked)
        Task<Enrollment?> GetEnrollmentForUpdate(Guid enrollmentId);

        // Upsert lesson progress (handles chapters internally)
        Task UpsertLessonProgress(Guid enrollmentId, Guid chapterId, Guid lessonId, bool isCompleted);

        // Upsert quiz progress (handles chapters/lessons internally)
        Task<(bool isCorrect, List<string> correctAnswers, string explanation)> UpsertQuizProgress(
            Guid enrollmentId,
            Guid chapterId,
            Guid lessonId,
            Guid quizId,
            List<string> submittedAnswers);

        Task<(
            int Total,
            int Completed,
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

        Task<List<(Guid CourseId, string CourseName, decimal EnrollmentCount)>>
        GetTopCoursesByEnrollment(
            DateTime? from,
            DateTime? to,
            Guid? gradeId,
            Guid? subjectId,
            int top
        );

        Task<List<(Guid SubjectId, string SubjectName, decimal EnrollmentCount)>>
        GetTopSubjectsByEnrollment(
            DateTime? from,
            DateTime? to,
            Guid? gradeId,
            int top
        );

        Task<List<(Guid GradeId, string GradeName, decimal EnrollmentCount)>>
        GetTopGradesByEnrollment(
            DateTime? from,
            DateTime? to,
            Guid? subjectId,
            int top
        );
    }
}
