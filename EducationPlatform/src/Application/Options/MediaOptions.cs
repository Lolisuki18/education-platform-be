namespace Application.Options
{
    public class MediaOptions
    {
        public const string SectionName = "Media";

        /// <summary>
        /// Secret that signs video URLs. Leave empty to derive one from <c>JwtSettings:SecretKey</c>;
        /// set it explicitly to rotate it independently of the JWT key.
        /// </summary>
        public string? SigningKey { get; set; }

        /// <summary>How long a signed video URL keeps working. Long enough to watch a lesson, short enough that a leaked link dies.</summary>
        public int UrlLifetimeMinutes { get; set; } = 240;

        /// <summary>
        /// When not empty, external lesson videos and materials must be hosted on one of these domains
        /// (subdomains included). Empty means any https host is accepted.
        /// </summary>
        public string[] AllowedExternalHosts { get; set; } = Array.Empty<string>();
    }
}
