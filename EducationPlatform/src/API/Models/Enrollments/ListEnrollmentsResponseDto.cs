using Application.Results;

namespace API.Models.Enrollments
{
    public class ListEnrollmentsResponseDto
    {
        public IEnumerable<EnrollmentDTO> Enrollments { get; set; } = new List<EnrollmentDTO>();
    }
}
