namespace API.Models.Enrollments
{
    public class UpdateQuizProgressResponseDto
    {
        public bool IsCorrect { get; set; }
        public string? Explanation { get; set; }
    }
}
