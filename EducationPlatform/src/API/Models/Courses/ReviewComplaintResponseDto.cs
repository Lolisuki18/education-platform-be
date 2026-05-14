using Application.Results;

namespace API.Models.Courses
{
    public class ReviewComplaintResponseDto
    {
        public string Message { get; set; } = string.Empty;
        public ComplaintDetailDTO Complaint { get; set; } = new();
        public CourseDetailDTO Course { get; set; } = new();
    }
}
