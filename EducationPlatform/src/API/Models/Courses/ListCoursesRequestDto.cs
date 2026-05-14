namespace API.Models.Courses
{
    public class ListCoursesRequestDto
    {
        public string? Title { get; set; }
        public string? GradeName { get; set; }
        public string? SubjectName { get; set; }
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 9;
    }
}
