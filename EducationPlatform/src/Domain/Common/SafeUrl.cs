namespace Domain.Common
{
    /// <summary>
    /// Decides which strings may be stored as a link to media: a teacher types them, and the frontend later
    /// renders them as video sources and download links, so schemes such as <c>javascript:</c> or <c>data:</c>
    /// must never get in.
    /// </summary>
    public static class SafeUrl
    {
        /// <summary>
        /// Accepts a path inside the platform storage (<c>videos/a.mp4</c>, <c>/videos/a.mp4</c>) or an absolute
        /// <c>https</c> URL without credentials. Everything else is refused.
        /// </summary>
        public static bool IsSafe(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var url = value.Trim();

            if (url.Length > 2000 || url.Any(c => char.IsControl(c) || char.IsWhiteSpace(c)))
                return false;

            if (url.Contains('\\'))
                return false;

            if (url.StartsWith("//", StringComparison.Ordinal))
                return false; // protocol-relative: the scheme would be inherited from the page

            // "/videos/a.mp4" is a path inside the storage. Do not hand it to Uri: on Linux .NET reads it as the
            // absolute URI file:///videos/a.mp4, on Windows it is not absolute at all.
            var isStoragePath = url.StartsWith('/');

            if (!isStoragePath && Uri.TryCreate(url, UriKind.Absolute, out var absolute))
            {
                return absolute.Scheme == Uri.UriSchemeHttps
                       && string.IsNullOrEmpty(absolute.UserInfo)
                       && !string.IsNullOrEmpty(absolute.Host);
            }

            // Relative path: a colon would make a browser read the first segment as a scheme
            if (url.Contains(':'))
                return false;

            return !url.Split('/').Any(segment => segment == "..");
        }

        /// <summary>The host of an absolute URL, or null for a storage path.</summary>
        public static string? HostOf(string value)
        {
            var url = value.Trim();

            // Only real web addresses have a host; a storage path such as /videos/a.mp4 must not (on Linux Uri
            // would parse it as file:///videos/a.mp4 with an empty host)
            if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return null;

            return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : null;
        }
    }
}
