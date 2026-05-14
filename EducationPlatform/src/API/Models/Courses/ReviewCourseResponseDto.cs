using Application.Results;

namespace API.Models.Courses
{
    public class ReviewCourseResponseDto
    {
        public string Message { get; set; } = string.Empty;
        public CourseDetailDTO Course { get; set; } = new();
        public IEnumerable<PolicyDTO> Policies { get; set; } = new List<PolicyDTO>();
    }
}
