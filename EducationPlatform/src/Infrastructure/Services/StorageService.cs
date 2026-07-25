using Application.Interface;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    public class StorageService : IStorageService
    {
        #region Attributes
        private readonly string root;
        private readonly Cloudinary? _cloudinary;
        private readonly ILogger<StorageService> _logger;
        #endregion

        #region Properties
        #endregion

        public StorageService(IConfiguration configuration, ILogger<StorageService> logger)
        {
            _logger = logger;
            root = configuration["Storage:RootPath"]
                   ?? throw new InvalidOperationException("Storage:RootPath is not configured");

            var cloudName = configuration["Cloudinary:CloudName"];
            var apiKey = configuration["Cloudinary:ApiKey"];
            var apiSecret = configuration["Cloudinary:ApiSecret"];

            if (!string.IsNullOrEmpty(cloudName) && !string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(apiSecret) &&
                cloudName != "YOUR_CLOUD_NAME" && apiKey != "YOUR_API_KEY" && apiSecret != "YOUR_API_SECRET")
            {
                var account = new Account(cloudName, apiKey, apiSecret);
                _cloudinary = new Cloudinary(account);
            }
        }

        #region Methods
        public async Task<string> SaveAsync(
            Stream file,
            string fileExtension,
            CancellationToken ct)
        {
            if (_cloudinary != null)
            {
                var ext = fileExtension.StartsWith(".") ? fileExtension : $".{fileExtension}";
                var isImage = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg", ".bmp" }
                    .Contains(ext.ToLower());

                if (isImage)
                {
                    var uploadParams = new ImageUploadParams
                    {
                        File = new FileDescription(Guid.NewGuid().ToString() + ext, file),
                        Folder = "education-platform/images"
                    };
                    var uploadResult = await _cloudinary.UploadAsync(uploadParams, ct);
                    if (uploadResult.Error != null)
                        throw new Exception($"Cloudinary upload failed: {uploadResult.Error.Message}");

                    return uploadResult.SecureUrl.ToString();
                }
                else
                {
                    var uploadParams = new RawUploadParams
                    {
                        File = new FileDescription(Guid.NewGuid().ToString() + ext, file),
                        Folder = "education-platform/raw"
                    };
                    var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                    if (uploadResult.Error != null)
                        throw new Exception($"Cloudinary upload failed: {uploadResult.Error.Message}");

                    return uploadResult.SecureUrl.ToString();
                }
            }

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
            if (!Guid.TryParse(uploadId, out _))
                throw new ArgumentException("Invalid uploadId format");

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
            if (!Guid.TryParse(uploadId, out _))
                throw new ArgumentException("Invalid uploadId format");

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
                .EnumerateFiles(tempDir)
                .Select(f => new { Path = f, FileName = Path.GetFileName(f) })
                .Where(x => int.TryParse(x.FileName, out _))
                .OrderBy(x => int.Parse(x.FileName))
                .Select(x => x.Path);

            foreach (var chunk in chunks)
            {
                await using var input = File.OpenRead(chunk);
                await input.CopyToAsync(output, ct);
            }

            Directory.Delete(tempDir, true);

            if (_cloudinary != null)
            {
                // Upload the completed file to Cloudinary
                var uploadParams = new VideoUploadParams
                {
                    File = new FileDescription(finalFullPath),
                    Folder = "education-platform/videos",
                    Transformation = new Transformation().Quality("auto").FetchFormat("auto")
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams, ct);

                // Clean up the local assembled file
                if (File.Exists(finalFullPath))
                {
                    File.Delete(finalFullPath);
                }

                if (uploadResult.Error != null)
                    throw new Exception($"Cloudinary video upload failed: {uploadResult.Error.Message}");

                return uploadResult.SecureUrl.ToString();
            }

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
            if (relativePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                relativePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return relativePath;
            }
            return Path.Combine(root, relativePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
        }

        public async Task DeleteAsync(string relativePath)
        {
            if (relativePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                relativePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                if (_cloudinary != null)
                {
                    try
                    {
                        var uri = new Uri(relativePath);
                        var segments = uri.Segments;

                        int uploadIndex = -1;
                        for (int i = 0; i < segments.Length; i++)
                        {
                            if (segments[i].Trim('/').Equals("upload", StringComparison.OrdinalIgnoreCase))
                            {
                                uploadIndex = i;
                                break;
                            }
                        }

                        if (uploadIndex != -1 && uploadIndex + 1 < segments.Length)
                        {
                            int startIndex = uploadIndex + 1;
                            if (segments[startIndex].StartsWith("v") && segments[startIndex].Length > 1 && char.IsDigit(segments[startIndex][1]))
                            {
                                startIndex++;
                            }

                            var publicIdWithExtension = string.Join("", segments.Skip(startIndex)).Trim('/');
                            var lastDot = publicIdWithExtension.LastIndexOf('.');
                            var publicId = lastDot != -1 ? publicIdWithExtension.Substring(0, lastDot) : publicIdWithExtension;

                            var isVideo = relativePath.Contains("/video/upload/");
                            var isRaw = relativePath.Contains("/raw/upload/");
                            var resourceType = isVideo ? ResourceType.Video : (isRaw ? ResourceType.Raw : ResourceType.Image);

                            var deletionParams = new DeletionParams(publicId)
                            {
                                ResourceType = resourceType
                            };
                            await _cloudinary.DestroyAsync(deletionParams);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[Storage] Failed to delete Cloudinary resource {RelativePath}", relativePath);
                    }
                }
                return;
            }

            var fullPath = GetFullPath(relativePath);
            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }
        #endregion
    }
}


