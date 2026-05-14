using Microsoft.AspNetCore.Http;

namespace API.Models.Courses
{
    public class CreateComplaintRequestDto
    {
        public Guid CourseId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public IFormFile? EvidenceImage { get; set; }
    }
}
