using Domain.EnrollmentManagement.Enum;

namespace Application.Results
{
    public class EnrollmentDTO
    {
        public Guid EnrollmentID { get; set; }
        public EnrollmentStatus Status { get; set; }
        public DateTime EnrolledAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public Guid StudentID { get; set; }
        public Guid CourseID { get; set; }
        public CourseProgressDTO CourseProgress { get; set; } = new CourseProgressDTO();
        public CourseDTO Course { get; set; } = new CourseDTO();
    }

    public class EnrollmentDetailDTO
    {
        public Guid EnrollmentID { get; set; }
        public EnrollmentStatus Status { get; set; }
        public DateTime EnrolledAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public Guid StudentID { get; set; }
        public Guid CourseID { get; set; }
        public CourseProgressDTO CourseProgress { get; set; } = new CourseProgressDTO();
        public CourseDetailDTO Course { get; set; } = new CourseDetailDTO();
    }

    public class CourseProgressDTO
    {
        public Guid CourseProgressID { get; set; }
        public decimal CompletionRate { get; set; }
        public bool IsCompleted { get; set; }
        public Guid EnrollmentID { get; set; }
        public List<ChapterProgressDTO> ChapterProgresses { get; set; } = new List<ChapterProgressDTO>();
    }

    public class ChapterProgressDTO
    {
        public Guid ChapterProgressID { get; set; }
        public decimal CompletionRate { get; set; }
        public bool IsCompleted { get; set; }
        public Guid ChapterID { get; set; }
        public List<LessonProgressDTO> LessonProgresses { get; set; } = new List<LessonProgressDTO>();
        public ChapterDTO Chapter { get; set; } = new ChapterDTO();
    }

    public class LessonProgressDTO
    {
        public Guid LessonProgressID { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime? CompletedAt { get; set; }
        public Guid CourseProgressID { get; set; }
        public Guid LessonID { get; set; }
        public List<QuizProgressDTO> QuizProgresses { get; set; } = new List<QuizProgressDTO>();
        public LessonDTO Lesson { get; set; } = new LessonDTO();
    }

    public class QuizProgressDTO
    {
        public Guid QuizProgressID { get; set; }
        public bool IsCorrect { get; set; }
        public int AttemptCount { get; set; }
        public DateTime? LastAttemptAt { get; set; }
        public Guid LessonProgressID { get; set; }
        public Guid QuizID { get; set; }
        public QuizDTO Quiz { get; set; } = new QuizDTO();
    }

    public class StudentWeaknessDTO
    {
        public Guid CourseId { get; set; }
        public string CourseTitle { get; set; } = string.Empty;
        public Guid ChapterId { get; set; }
        public Guid LessonId { get; set; }
        public string LessonTitle { get; set; } = string.Empty;
        public decimal CompletionRate { get; set; }
        public int FailedQuizCount { get; set; }
    }
}


