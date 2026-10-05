using System.Security.Cryptography;
using System.Text;
using Domain.Exceptions;

namespace Domain.IdentityManagement.Entity
{
    /// <summary>
    /// One refresh-token session (one device / login). Only the SHA-256 hash of the token is stored.
    /// </summary>
    public class RefreshSession
    {
        /// <summary>
        /// A token that was rotated this recently is treated as a benign retry (flaky network, two tabs)
        /// instead of a stolen-token replay.
        /// </summary>
        public static readonly TimeSpan ReuseGracePeriod = TimeSpan.FromSeconds(10);

        public Guid SessionID { get; private set; }
        public Guid UserID { get; private set; }
        public string Hash { get; private set; } = string.Empty;
        public DateTime CreatedAt { get; private set; }
        public DateTime ExpiresAt { get; private set; }
        public DateTime? RevokedAt { get; private set; }

        protected RefreshSession() { }

        public RefreshSession(Guid userId, string plainText, TimeSpan lifetime)
        {
            if (string.IsNullOrWhiteSpace(plainText))
                throw new DomainException("Refresh token cannot be empty");

            if (lifetime <= TimeSpan.Zero)
                throw new DomainException("Refresh token lifetime must be greater than zero");

            SessionID = Guid.NewGuid();
            UserID = userId;
            Hash = HashToken(plainText);
            CreatedAt = DateTime.UtcNow;
            ExpiresAt = CreatedAt.Add(lifetime);
        }

        public bool IsRevoked => RevokedAt.HasValue;

        public bool IsExpired => ExpiresAt <= DateTime.UtcNow;

        public bool IsActive => !IsRevoked && !IsExpired;

        public bool Matches(string plainText) => Hash == HashToken(plainText);

        /// <summary>True when this session was revoked long enough ago that presenting it again is suspicious.</summary>
        public bool IsReplay => RevokedAt.HasValue && DateTime.UtcNow - RevokedAt.Value > ReuseGracePeriod;

        public void Revoke()
        {
            RevokedAt ??= DateTime.UtcNow;
        }

        public static string HashToken(string value)
        {
            return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        }
    }
}
