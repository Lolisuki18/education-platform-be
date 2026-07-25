using Microsoft.AspNetCore.Http;

namespace API.Models.Courses
{
    public class CreateCourseDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal? Price { get; set; }
        public string ThumbnailName { get; set; } = string.Empty;
        public IFormFile? ThumbnailFile { get; set; }
        public string? Slug { get; set; } = string.Empty;
        public string Prerequisites { get; set; } = string.Empty;
        public string LearningOutcomes { get; set; } = string.Empty;
        public Guid GradeID { get; set; }
        public Guid SubjectID { get; set; }
        public List<CreateChapterDto> Chapters { get; set; } = new List<CreateChapterDto>();
    }

    public class CreateChapterDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Order { get; set; }
        public List<CreateLessonDto> Lessons { get; set; } = new List<CreateLessonDto>();
    }

    public class CreateLessonDto
    {
        public string Title { get; set; } = string.Empty;
        public string Objectives { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string VideoUrl { get; set; } = string.Empty;
        public int Order { get; set; }
        public List<CreateQuizDto> Quizzes { get; set; } = new List<CreateQuizDto>();
        public List<CreateAssignmentDto> Assignments { get; set; } = new List<CreateAssignmentDto>();
        public List<CreateMaterialDto> Materials { get; set; } = new List<CreateMaterialDto>();
    }

    public class CreateQuizDto
    {
        public string Question { get; set; } = string.Empty;
        public string? Note { get; set; }
        public CreateQuizAnswerDto Answer { get; set; } = new CreateQuizAnswerDto();
    }

    public class CreateQuizAnswerDto
    {
        public int Type { get; set; } = 1;
        public List<string>? CorrectAnswers { get; set; } = new List<string>();
        public List<string>? Options { get; set; } = new List<string>();
        public bool? TrueOrFalse { get; set; } = false;
    }

    public class CreateAssignmentDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int MaxScore { get; set; }
    }

    public class CreateMaterialDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public Domain.CourseManagement.Enum.MaterialType Type { get; set; }
    }
}
