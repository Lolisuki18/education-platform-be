namespace Application.Interface
{
    public interface IStorageService
    {
        Task<string> SaveAsync(
            Stream file,
            string fileExtension,
            CancellationToken ct);

        /// <summary>
        /// Stores one chunk of a resumable upload. The first chunk binds the upload to <paramref name="ownerId"/>;
        /// chunks from anybody else are rejected.
        /// </summary>
        Task SaveChunkAsync(
            Stream chunk,
            string uploadId,
            int chunkIndex,
            Guid ownerId,
            CancellationToken ct
        );

        Task<string> CompleteUploadAsync(
            string uploadId,
            string extension,
            Guid ownerId,
            CancellationToken ct
        );

        Task<string> GetTranscriptFromVideoAsync(
            string videoRelativePath,
            CancellationToken ct);

        string GetFullPath(string relativePath);

        Task DeleteAsync(string relativePath);
    }
}

