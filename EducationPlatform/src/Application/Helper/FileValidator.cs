using System;
using System.IO;
using System.Linq;
using Application.BusinessException;

namespace Application.Helper
{
    public static class FileValidator
    {
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".pdf", ".mp4", ".doc", ".docx", ".zip" };
        private const long MaxFileSize = 100 * 1024 * 1024; // 100MB

        public static void Validate(Stream? stream, long length, string fileName)
        {
            if (stream == null || length == 0)
                return;

            if (length > MaxFileSize)
                throw new BadRequest($"File size exceeds the limit of {MaxFileSize / 1024 / 1024}MB.");

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
                throw new BadRequest("Invalid file type.");

            // Verify magic bytes (signature check)
            if (stream.CanSeek)
            {
                var position = stream.Position;
                var header = new byte[8];
                int bytesRead = stream.Read(header, 0, header.Length);
                stream.Position = position; // Reset stream position

                if (bytesRead >= 4)
                {
                    bool isValid = false;

                    // JPEG: FF D8 FF
                    if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) isValid = true;
                    // PNG: 89 50 4E 47
                    else if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47) isValid = true;
                    // PDF: 25 50 44 46
                    else if (header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46) isValid = true;
                    // ZIP / DOCX / OpenXML: 50 4B 03 04 (PK..)
                    else if (header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04) isValid = true;
                    // OLE2 / DOC: D0 CF 11 E0
                    else if (header[0] == 0xD0 && header[1] == 0xCF && header[2] == 0x11 && header[3] == 0xE0) isValid = true;
                    // MP4 / QuickTime: check for 'ftyp' at offset 4
                    else if (bytesRead >= 8 && header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70) isValid = true;

                    if (!isValid)
                        throw new BadRequest("File content does not match the allowed extension types.");
                }
            }
        }
    }
}
