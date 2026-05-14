namespace API.Models.Auth
{
    public class VerifyEmailRequestDto
    {
        public string Otp { get; set; } = string.Empty;
    }
}
