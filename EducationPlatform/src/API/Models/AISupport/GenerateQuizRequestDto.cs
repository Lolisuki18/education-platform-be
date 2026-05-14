namespace API.Models.AISupport
{
    public class GenerateQuizRequestDto
    {
        public Guid EnrollmentId { get; set; }
        public Guid ChapterId { get; set; }
        public Guid LessonId { get; set; }
    }
}
