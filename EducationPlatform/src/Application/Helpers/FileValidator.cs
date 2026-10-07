using System;
using System.IO;
using System.Linq;
using Application.Exceptions;

namespace Application.Helpers
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
                throw new BadRequestException($"File size exceeds the limit of {MaxFileSize / 1024 / 1024}MB.");

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
                throw new BadRequestException("Invalid file type.");

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
                        throw new BadRequestException("File content does not match the allowed extension types.");
                }
            }
        }

        /// <summary>
        /// True when the first bytes of a finished upload are those of the video container its extension claims:
        /// MP4/MOV have an atom name (usually <c>ftyp</c>) at offset 4, WebM/MKV start with the EBML marker.
        /// Only the whole file can be judged this way; a single chunk is an arbitrary slice of it.
        /// </summary>
        public static bool HasVideoSignature(ReadOnlySpan<byte> header, string extension)
        {
            switch (extension.TrimStart('.').ToLowerInvariant())
            {
                case "mp4":
                case "mov":
                    if (header.Length < 8)
                        return false;
                    var atom = System.Text.Encoding.ASCII.GetString(header.Slice(4, 4));
                    return atom is "ftyp" or "moov" or "mdat" or "wide" or "free" or "skip" or "pnot";

                case "webm":
                case "mkv":
                    return header.Length >= 4 && header[0] == 0x1A && header[1] == 0x45 && header[2] == 0xDF && header[3] == 0xA3;

                default:
                    return false;
            }
        }
    }
}
