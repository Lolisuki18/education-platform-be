namespace API.Models.Enrollments
{
    public class UpdateLessonProgressRequestDto
    {
        public Guid EnrollmentId { get; set; }
        public Guid ChapterId { get; set; }
        public Guid LessonId { get; set; }
        public double PlayedSeconds { get; set; }
        public double Duration { get; set; }
        public bool IsCompleted { get; set; }
    }
}
