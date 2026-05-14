using Application.Interface;
using Microsoft.Extensions.Configuration;

namespace Application.Implementation
{
    public class StorageService : IStorageService
    {
        #region Attributes
        private readonly string root;
        #endregion

        #region Properties
        #endregion

        public StorageService()
        {
            // Long note: Not clean architecture
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            root = configuration["Storage:RootPath"]
                   ?? throw new InvalidOperationException("Storage:RootPath is not configured");
        }

        #region Methods
        public async Task<string> SaveAsync(
            Stream file,
            string fileExtension,
            CancellationToken ct)
        {
            var now = DateTime.UtcNow;

            var relativePath = Path.Combine(
                now.Year.ToString(),
                now.Month.ToString("D2"),
                $"{Guid.NewGuid()}.{fileExtension.TrimStart('.')}"
            );

            var fullPath = Path.Combine(root, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            using var output = File.Create(fullPath);
            await file.CopyToAsync(output, ct);

            return relativePath.Replace("\\", "/");
        }

        public async Task SaveChunkAsync(
            Stream chunk,
            string uploadId,
            int chunkIndex,
            CancellationToken ct)
        {
            var tempDir = Path.Combine(root, "temp", uploadId);
            Directory.CreateDirectory(tempDir);

            var chunkPath = Path.Combine(tempDir, chunkIndex.ToString());

            await using var output = new FileStream(
                chunkPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                1024 * 1024,
                useAsync: true
            );

            await chunk.CopyToAsync(output, ct);
        }

        public async Task<string> CompleteUploadAsync(
            string uploadId,
            string extension,
            CancellationToken ct)
        {
            var tempDir = Path.Combine(root, "temp", uploadId);

            if (!Directory.Exists(tempDir))
                throw new InvalidOperationException("Upload not found");

            var finalRelativePath = Path.Combine(
                "videos",
                $"{uploadId}.{extension}"
            );

            var finalFullPath = Path.Combine(root, finalRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(finalFullPath)!);

            await using var output = new FileStream(
                finalFullPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                1024 * 1024,
                true
            );

            var chunks = Directory
                .GetFiles(tempDir)
                .OrderBy(f => int.Parse(Path.GetFileName(f)));

            foreach (var chunk in chunks)
            {
                await using var input = File.OpenRead(chunk);
                await input.CopyToAsync(output, ct);
            }

            Directory.Delete(tempDir, true);

            return finalRelativePath.Replace("\\", "/");
        }

        public async Task<string> GetTranscriptFromVideoAsync(
            string videoRelativePath,
            CancellationToken ct)
        {
            // Change extension to .txt
            var transcriptRelativePath = Path.ChangeExtension(videoRelativePath, ".txt");

            var fullPath = GetFullPath(transcriptRelativePath);

            if (!File.Exists(fullPath))
                throw new FileNotFoundException("Transcript not found", transcriptRelativePath);

            using var reader = new StreamReader(fullPath);
            return await reader.ReadToEndAsync(ct);
        }

        public string GetFullPath(string relativePath)
        {
            return Path.Combine(root, relativePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
        }
        #endregion
    }
}


