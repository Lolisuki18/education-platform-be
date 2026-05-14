using Application.Results;

namespace API.Models.AISupport
{
    public class AISupportLoadResponseDto
    {
        public List<EnrollmentDTO> Enrollments { get; set; } = new();
        public Guid? SelectedEnrollmentId { get; set; }
        public CourseDetailDTO? SelectedCourse { get; set; }
        public List<ChapterDTO> Chapters { get; set; } = new();
        public Guid? SelectedChapterId { get; set; }
        public List<LessonDTO> Lessons { get; set; } = new();
        public Guid? SelectedLessonId { get; set; }
        public LessonDTO? SelectedLesson { get; set; }
    }
}
