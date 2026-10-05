namespace Application.Common
{
    /// <summary>
    /// Builds the argument for a case-insensitive "contains" search that PostgreSQL can answer from a trigram
    /// index: <c>EF.Functions.Like(column.ToLower(), SearchPattern.Contains(term), SearchPattern.EscapeCharacter)</c>
    /// becomes <c>lower(column) LIKE '%term%'</c>, which an index on <c>lower(column)</c> serves (a plain
    /// <c>string.Contains</c> is translated to <c>strpos</c>, which no index can help with).
    /// </summary>
    public static class SearchPattern
    {
        public const string EscapeCharacter = "\\";

        /// <summary>Matches rows whose value contains <paramref name="term"/> anywhere, ignoring case.</summary>
        public static string Contains(string term)
        {
            var escaped = term
                .Trim()
                .ToLowerInvariant()
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_");

            return $"%{escaped}%";
        }
    }
}
