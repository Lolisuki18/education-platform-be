namespace API.Models.Academic
{
    public class CreateGradeRequestDto
    {
        public string Name { get; set; } = string.Empty;
    }

    public class UpdateGradeRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class CreateSubjectRequestDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class UpdateSubjectRequestDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
