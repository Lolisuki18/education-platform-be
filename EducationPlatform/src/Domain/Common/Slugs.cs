using System.Globalization;
using System.Text;

namespace Domain.Common
{
    /// <summary>URL-friendly names for courses: lowercase ASCII words joined by dashes ("Toán 10" becomes "toan-10").</summary>
    public static class Slugs
    {
        /// <summary>Room is left below the column limit so a uniqueness suffix can be appended.</summary>
        public const int MaxLength = 200;

        private const int SuffixRoom = 9; // "-" + 8 characters

        /// <summary>Normalises <paramref name="slug"/> when it has usable content, otherwise builds one from <paramref name="title"/>.</summary>
        public static string Create(string? slug, string title)
        {
            var normalized = Normalize(slug);
            if (normalized.Length == 0)
                normalized = Normalize(title);

            return normalized.Length == 0 ? "course" : normalized;
        }

        public static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var builder = new StringBuilder(value.Length);
            var lastWasDash = true; // swallows leading separators

            foreach (var c in value.Normalize(NormalizationForm.FormD))
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (category == UnicodeCategory.NonSpacingMark)
                    continue; // the accent of a decomposed letter

                var ch = c switch { 'đ' => 'd', 'Đ' => 'd', _ => char.ToLowerInvariant(c) };

                if (ch is >= 'a' and <= 'z' or >= '0' and <= '9')
                {
                    builder.Append(ch);
                    lastWasDash = false;
                }
                else if (!lastWasDash)
                {
                    builder.Append('-');
                    lastWasDash = true;
                }
            }

            var result = builder.ToString().Trim('-');
            return result.Length > MaxLength - SuffixRoom ? result[..(MaxLength - SuffixRoom)].Trim('-') : result;
        }

        /// <summary>Makes a taken slug unique by appending a short random part.</summary>
        public static string WithSuffix(string slug)
        {
            return $"{slug}-{Guid.NewGuid().ToString("N")[..8]}";
        }
    }
}
