namespace Application.Options
{
    /// <summary>Limits for chunked video uploads. Every value can be overridden in the "Upload" configuration section.</summary>
    public class UploadOptions
    {
        public const string SectionName = "Upload";

        /// <summary>Hard cap on one HTTP request carrying a chunk (chunk + multipart overhead).</summary>
        public const long RequestSizeLimitBytes = 33 * 1024 * 1024;

        public long MaxChunkBytes { get; set; } = 32 * 1024 * 1024;

        public int MaxChunksPerUpload { get; set; } = 2000;

        /// <summary>Total size of all chunks of one upload, so a single upload cannot fill the disk.</summary>
        public long MaxTotalBytesPerUpload { get; set; } = 2L * 1024 * 1024 * 1024;
    }
}
