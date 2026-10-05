namespace Application.Options
{
    public class EmailOptions
    {
        public const string SectionName = "EmailSettings";

        public string From { get; set; } = "noreply@educationplatform.com";

        public string DisplayName { get; set; } = "Education Platform";

        public string SmtpHost { get; set; } = "localhost";

        public int SmtpPort { get; set; } = 25;

        public string Username { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public bool EnableSSL { get; set; }
    }
}
