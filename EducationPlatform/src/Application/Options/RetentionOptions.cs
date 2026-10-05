namespace Application.Options
{
    public class RetentionOptions
    {
        public const string SectionName = "Retention";

        public bool Enabled { get; set; } = true;

        /// <summary>How long expired or revoked refresh sessions are kept (replay detection needs the recent ones).</summary>
        public int RefreshSessionGraceDays { get; set; } = 7;

        public int AuditLogDays { get; set; } = 365;

        /// <summary>Accounts that never verified their e-mail are removed after this many days.</summary>
        public int UnverifiedUserDays { get; set; } = 7;
    }
}
