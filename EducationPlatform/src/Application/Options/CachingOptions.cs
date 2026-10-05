namespace Application.Options
{
    public class CachingOptions
    {
        public const string SectionName = "Caching";

        /// <summary>Switches the short-lived cache of expensive reports (statistics) on or off.</summary>
        public bool Enabled { get; set; } = true;
    }
}
