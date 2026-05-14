using Application.Results;

namespace API.Models.Enrollments
{
    public class ResumeEnrollmentResponseDto
    {
        public EnrollmentDetailDTO Enrollment { get; set; } = new();
    }
}
