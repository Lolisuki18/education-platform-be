using Application.Results;

namespace API.Models.Courses
{
    public class ListCoursesResponseDto
    {
        public IEnumerable<CourseDTO> Courses { get; set; } = new List<CourseDTO>();
        public IEnumerable<GradeDTO> Grades { get; set; } = new List<GradeDTO>();
        public IEnumerable<SubjectDTO> Subjects { get; set; } = new List<SubjectDTO>();
    }
}
