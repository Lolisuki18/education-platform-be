// Remove legacy using Application.Commands.Course;

namespace API.Models.Courses
{
    public class CreateCourseResponseDto
    {
        public Guid CourseID { get; set; }
        public string Message { get; set; } = string.Empty;
        public CreateCourseDto Course { get; set; } = new();
    }
}
