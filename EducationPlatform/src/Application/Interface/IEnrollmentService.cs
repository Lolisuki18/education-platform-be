using Application.Results;

namespace Application.Interface
{
    public interface IEnrollmentService
    {
        Task<IEnumerable<EnrollmentDTO>> GetStudentEnrollments(
            Guid studentId);

        Task<EnrollmentDetailDTO> GetEnrollmentDetail(
            Guid enrollmentId);

        Task<IEnumerable<StudentWeaknessDTO>> GetEnrollmentWeakness(
            Guid enrollmentId);

        Task UpdateLessonProgress(
            Guid enrollmentId,
            Guid chapterId,
            Guid lessonId,
            double playedSeconds,
            double duration,
            bool isCompleted,
            Guid callerId);

        Task<(bool isCorrect, string explanation)> UpdateQuizProgress(
            Guid enrollmentId,
            Guid chapterId,
            Guid lessonId,
            Guid quizId,
            List<string> selectedAnswers,
            Guid callerId);
    }
}


