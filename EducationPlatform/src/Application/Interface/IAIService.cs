namespace Application.Interface
{
    public interface IAIService
    {
        Task<string> GenerateQuizAsync(
            string grade,
            string subject,
            string transcript);
    }
}


