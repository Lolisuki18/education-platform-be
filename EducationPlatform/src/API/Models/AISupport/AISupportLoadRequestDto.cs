namespace API.Models.AISupport
{
    public class AISupportLoadRequestDto
    {
        public Guid? SelectedEnrollmentId { get; set; }
        public Guid? SelectedChapterId { get; set; }
        public Guid? SelectedLessonId { get; set; }
    }
}
