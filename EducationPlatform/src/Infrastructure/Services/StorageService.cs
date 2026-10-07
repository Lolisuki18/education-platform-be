using Application.Interface;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Application.Exceptions;
using Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services
{
    public class StorageService : IStorageService
    {
        #region Attributes
        private readonly string root;
        private readonly Cloudinary? _cloudinary;
        private readonly ILogger<StorageService> _logger;
        private readonly UploadOptions _uploadOptions;

        private const string OwnerMarkerFileName = "owner";

        private static readonly HashSet<string> AllowedCompleteUploadExtensions =
            new(StringComparer.OrdinalIgnoreCase) { "mp4", "mov", "webm", "mkv" };
        #endregion

        #region Properties
        #endregion

        public StorageService(IConfiguration configuration, ILogger<StorageService> logger, IOptions<UploadOptions> uploadOptions)
        {
            _logger = logger;
            _uploadOptions = uploadOptions.Value;
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
            Guid ownerId,
            CancellationToken ct)
        {
            if (!Guid.TryParse(uploadId, out _))
                throw new BadRequestException("Invalid uploadId format");

            if (chunkIndex < 0 || chunkIndex >= _uploadOptions.MaxChunksPerUpload)
                throw new BadRequestException($"Chunk index must be between 0 and {_uploadOptions.MaxChunksPerUpload - 1}.");

            var chunkLength = chunk.CanSeek ? chunk.Length : 0;
            if (chunkLength > _uploadOptions.MaxChunkBytes)
                throw new BadRequestException($"A chunk cannot be larger than {_uploadOptions.MaxChunkBytes / 1024 / 1024}MB.");

            var tempDir = Path.Combine(root, "temp", uploadId);
            Directory.CreateDirectory(tempDir);

            await EnsureOwnerAsync(tempDir, ownerId, ct);

            var chunkPath = Path.Combine(tempDir, chunkIndex.ToString());

            // Re-sending a chunk replaces it, so it must not count twice
            var storedBytes = Directory.EnumerateFiles(tempDir)
                .Where(f => !string.Equals(f, chunkPath, StringComparison.Ordinal))
                .Sum(f => new FileInfo(f).Length);
            if (storedBytes + chunkLength > _uploadOptions.MaxTotalBytesPerUpload)
                throw new BadRequestException($"An upload cannot be larger than {_uploadOptions.MaxTotalBytesPerUpload / 1024 / 1024}MB.");

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

        /// <summary>The first chunk claims the upload; every later request must come from the same user.</summary>
        private static async Task EnsureOwnerAsync(string tempDir, Guid ownerId, CancellationToken ct)
        {
            var markerPath = Path.Combine(tempDir, OwnerMarkerFileName);

            if (!File.Exists(markerPath))
            {
                try
                {
                    await using var marker = new FileStream(markerPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                    await marker.WriteAsync(System.Text.Encoding.UTF8.GetBytes(ownerId.ToString()), ct);
                    return;
                }
                catch (IOException)
                {
                    // Another request claimed the upload first: fall through and compare owners
                }
            }

            await AssertOwnerAsync(tempDir, ownerId, ct);
        }

        private static async Task AssertOwnerAsync(string tempDir, Guid ownerId, CancellationToken ct)
        {
            var markerPath = Path.Combine(tempDir, OwnerMarkerFileName);

            // Uploads started before owners were recorded have no marker and stay usable
            if (!File.Exists(markerPath))
                return;

            var owner = (await File.ReadAllTextAsync(markerPath, ct)).Trim();
            if (!string.Equals(owner, ownerId.ToString(), StringComparison.OrdinalIgnoreCase))
                throw new ForbiddenException("This upload belongs to another user.");
        }

        public async Task<string> CompleteUploadAsync(
            string uploadId,
            string extension,
            Guid ownerId,
            CancellationToken ct)
        {
            if (!Guid.TryParse(uploadId, out _))
                throw new BadRequestException("Invalid uploadId format");

            var normalizedExtension = extension.TrimStart('.');
            if (!AllowedCompleteUploadExtensions.Contains(normalizedExtension))
                throw new BadRequestException("Invalid or unsupported file extension.");

            var tempDir = Path.Combine(root, "temp", uploadId);

            if (!Directory.Exists(tempDir))
                throw new NotFoundException("Upload not found");

            await AssertOwnerAsync(tempDir, ownerId, ct);

            var finalRelativePath = Path.Combine(
                "videos",
                $"{uploadId}.{normalizedExtension}"
            );

            var finalFullPath = Path.Combine(root, finalRelativePath);

            var chunks = Directory
                .EnumerateFiles(tempDir)
                .Select(f => new { Path = f, FileName = Path.GetFileName(f) })
                .Where(x => int.TryParse(x.FileName, out _))
                .OrderBy(x => int.Parse(x.FileName))
                .Select(x => x.Path)
                .ToList();

            if (chunks.Count == 0)
                throw new BadRequestException("The upload contains no data.");

            // Chunks are raw slices, so the file can only be recognised as a video once it is whole: judge its first bytes
            var header = new byte[16];
            await using (var first = File.OpenRead(chunks[0]))
            {
                var read = await first.ReadAsync(header.AsMemory(0, header.Length), ct);
                if (!Application.Helpers.FileValidator.HasVideoSignature(header.AsSpan(0, read), normalizedExtension))
                    throw new BadRequestException("The uploaded file is not a valid video.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(finalFullPath)!);

            await using var output = new FileStream(
                finalFullPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                1024 * 1024,
                true
            );

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


