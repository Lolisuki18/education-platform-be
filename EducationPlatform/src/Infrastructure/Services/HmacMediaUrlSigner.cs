using System.Security.Cryptography;
using System.Text;
using Application.Interface;
using Application.Options;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services
{
    public class HmacMediaUrlSigner : IMediaUrlSigner
    {
        /// <summary>Folder (inside the storage root) that holds uploaded lesson videos.</summary>
        public const string ProtectedFolder = "videos";

        private readonly byte[] _key;
        private readonly TimeSpan _lifetime;
        private readonly TimeProvider _time;

        public HmacMediaUrlSigner(IOptions<MediaOptions> media, IOptions<JwtOptions> jwt, TimeProvider time)
        {
            _time = time;
            _lifetime = TimeSpan.FromMinutes(Math.Max(1, media.Value.UrlLifetimeMinutes));

            // A purpose-specific key: tokens signed for URLs are useless as anything else even if the JWT key is shared
            var secret = string.IsNullOrWhiteSpace(media.Value.SigningKey) ? jwt.Value.SecretKey : media.Value.SigningKey;
            _key = new HMACSHA256(Encoding.UTF8.GetBytes(secret)).ComputeHash(Encoding.UTF8.GetBytes("media-url-signing"));
        }

        public string Protect(string url)
        {
            if (string.IsNullOrWhiteSpace(url) || !TryGetProtectedPath(url, out var path))
                return url;

            var expires = _time.GetUtcNow().Add(_lifetime).ToUnixTimeSeconds();
            var separator = url.Contains('?') ? '&' : '?';

            return $"{url}{separator}exp={expires}&sig={Sign(path, expires)}";
        }

        public bool IsValid(string relativePath, long expires, string signature)
        {
            if (string.IsNullOrEmpty(signature) || expires < _time.GetUtcNow().ToUnixTimeSeconds())
                return false;

            var expected = Encoding.ASCII.GetBytes(Sign(Normalize(relativePath), expires));
            var actual = Encoding.ASCII.GetBytes(signature);

            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }

        /// <summary>True for a platform-hosted path inside the protected folder, in either <c>videos/x</c> or <c>/videos/x</c> form.</summary>
        public static bool TryGetProtectedPath(string url, out string path)
        {
            path = string.Empty;

            // External URLs are somebody else's responsibility
            if (url.Contains("://", StringComparison.Ordinal))
                return false;

            var withoutQuery = url.Split('?', 2)[0];
            var normalized = Normalize(withoutQuery);

            if (!normalized.StartsWith(ProtectedFolder + "/", StringComparison.OrdinalIgnoreCase))
                return false;

            path = normalized;
            return true;
        }

        /// <summary>One canonical spelling per file, so a signature cannot be dodged with <c>//</c> or a leading slash.</summary>
        public static string Normalize(string path)
        {
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var start = segments.Length > 0 && segments[0].Equals("media", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            return string.Join('/', segments.Skip(start));
        }

        private string Sign(string path, long expires)
        {
            using var hmac = new HMACSHA256(_key);
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{path}\n{expires}"));

            // URL-safe, no padding
            return Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }
}
