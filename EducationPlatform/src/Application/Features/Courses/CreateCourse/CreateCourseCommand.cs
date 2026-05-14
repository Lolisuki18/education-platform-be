using Domain.CourseManagement.Enum;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace Application.Features.Courses.CreateCourse
{
    public class CreateCourseCommand : IRequest<Guid>
    {
        public Guid CallerId { get; set; }
        public string CallerRole { get; set; } = string.Empty;

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

        public List<CreateChapterCommandDto> Chapters { get; set; } = new();
    }

    public class CreateChapterCommandDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Order { get; set; }
        public List<CreateLessonCommandDto> Lessons { get; set; } = new();
    }

    public class CreateLessonCommandDto
    {
        public string Title { get; set; } = string.Empty;
        public string Objectives { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string VideoUrl { get; set; } = string.Empty;
        public int Order { get; set; }
        public List<CreateQuizCommandDto> Quizzes { get; set; } = new();
        public List<CreateAssignmentCommandDto> Assignments { get; set; } = new();
        public List<CreateMaterialCommandDto> Materials { get; set; } = new();
    }

    public class CreateQuizCommandDto
    {
        public string Question { get; set; } = string.Empty;
        public string? Note { get; set; }
        public CreateQuizAnswerCommandDto Answer { get; set; } = new();
    }

    public class CreateQuizAnswerCommandDto
    {
        public int Type { get; set; } = 1;
        public List<string>? CorrectAnswers { get; set; } = new();
        public List<string>? Options { get; set; } = new();
        public bool? TrueOrFalse { get; set; } = false;
    }

    public class CreateAssignmentCommandDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int MaxScore { get; set; }
    }

    public class CreateMaterialCommandDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public MaterialType Type { get; set; }
    }
}
