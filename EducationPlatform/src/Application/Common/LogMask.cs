using System.Text.RegularExpressions;

namespace Application.Common
{
    /// <summary>Keeps personal data out of log files: they are copied to many places and kept for a long time.</summary>
    public static partial class LogMask
    {
        /// <summary><c>jane.doe@gmail.com</c> becomes <c>j***@gmail.com</c>: enough to recognise a case, not enough to contact anyone.</summary>
        public static string Email(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return string.Empty;

            var at = email.IndexOf('@');
            if (at <= 0)
                return "***";

            return $"{email[0]}***{email[at..]}";
        }

        /// <summary>Masks every e-mail address inside free text, e.g. an exception message.</summary>
        public static string Scrub(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return text ?? string.Empty;

            return EmailPattern().Replace(text, m => Email(m.Value));
        }

        [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}")]
        private static partial Regex EmailPattern();
    }
}
