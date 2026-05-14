namespace API.Models.Enrollments
{
    public class UpdateQuizProgressRequestDto
    {
        public List<string> SelectedAnswers { get; set; } = new();
        public Guid EnrollmentId { get; set; }
        public Guid ChapterId { get; set; }
        public Guid LessonId { get; set; }
        public Guid QuizId { get; set; }
    }
}
