using Microsoft.AspNetCore.Http;
using Application.BusinessException;

namespace Application.Helper
{
    public static class FileValidator
    {
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".pdf", ".mp4", ".doc", ".docx", ".zip" };
        private const long MaxFileSize = 100 * 1024 * 1024; // 100MB

        public static void Validate(IFormFile? file)
        {
            if (file == null || file.Length == 0)
                return;

            if (file.Length > MaxFileSize)
                throw new BadRequest($"File size exceeds the limit of {MaxFileSize / 1024 / 1024}MB.");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
                throw new BadRequest("Invalid file type.");
        }
    }
}
