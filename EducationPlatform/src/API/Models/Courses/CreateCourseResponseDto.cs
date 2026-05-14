// Remove legacy using Application.Commands.Course;

namespace API.Models.Courses
{
    public class CreateCourseResponseDto
    {
        public string Message { get; set; } = string.Empty;
        public CreateCourseDto Course { get; set; } = new();
    }
}
