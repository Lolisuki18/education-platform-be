namespace Application.Results
{
    public class GradeDTO
    {
        public Guid GradeID { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class SubjectDTO
    {
        public Guid SubjectID { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class DefaultLessonDTO
    {
        public Guid DefaultLessonID { get; set; }
        public string Objectives { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }

        public Guid SubjectID { get; set; }
        public Guid GradeID { get; set; }
    }
}


