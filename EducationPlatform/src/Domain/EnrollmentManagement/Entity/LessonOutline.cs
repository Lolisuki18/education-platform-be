namespace Domain.EnrollmentManagement.Entity
{
    /// <summary>One lesson of a course as the course defines it: where it sits and how many quizzes it has.</summary>
    public sealed record LessonOutline(Guid LessonID, Guid ChapterID, int QuizCount);
}
