using Microsoft.AspNetCore.Http;

namespace API.Models.Courses
{
    public class CreateCourseRequestDto
    {
        public CreateCourseDto CreateCourse { get; set; } = new();
        public IFormFile Thumbnail { get; set; } = default!;
    }
}
