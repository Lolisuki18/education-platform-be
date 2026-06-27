namespace Application.Interface
{
    public interface IStorageService
    {
        Task<string> SaveAsync(
            Stream file,
            string fileExtension,
            CancellationToken ct);

        Task SaveChunkAsync(
            Stream chunk,
            string uploadId,
            int chunkIndex,
            CancellationToken ct
        );

        Task<string> CompleteUploadAsync(
            string uploadId,
            string extension,
            CancellationToken ct
        );

        Task<string> GetTranscriptFromVideoAsync(
            string videoRelativePath,
            CancellationToken ct);

        string GetFullPath(string relativePath);

        Task DeleteAsync(string relativePath);
    }
}

