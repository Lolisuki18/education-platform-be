using System.Security.Cryptography;
using System.Text;
using Domain.DomainExceptions;

namespace Domain.IdentityManagement.ValueObject
{
    public sealed class RefreshToken
    {
        #region Attributes
        #endregion

        #region Properties
        public string Hash { get; }
        public DateTime ExpiresAt { get; }
        #endregion

        private RefreshToken(
            string hash,
            DateTime expiresAt)
        {
            Hash = hash;
            ExpiresAt = expiresAt;
        }

        #region Methods
        public static RefreshToken Create(
            string plainText,
            TimeSpan lifetime)
        {
            if (string.IsNullOrWhiteSpace(plainText))
                throw new DomainException(
                    "Refresh token cannot be empty");

            if (lifetime <= TimeSpan.Zero)
                throw new DomainException(
                    "Refresh token lifetime must be greater than zero");

            var hash = HashToken(plainText);
            return new RefreshToken(
                hash,
                DateTime.UtcNow.Add(lifetime)
            );
        }

        public bool Verify(string plainText)
        {
            return Hash == HashToken(plainText)
                   && ExpiresAt > DateTime.UtcNow;
        }

        public bool IsValid(string plainToken)
        {
            return DateTime.UtcNow <= ExpiresAt
                && BCrypt.Net.BCrypt.Verify(plainToken, Hash);
        }

        private static string HashToken(string value)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
            return Convert.ToBase64String(bytes);
        }

        public override bool Equals(object? obj)
        {
            return obj is RefreshToken other &&
                   Hash == other.Hash &&
                   ExpiresAt == other.ExpiresAt;
        }

        public override int GetHashCode()
        {
            return Hash.GetHashCode();
        }
        #endregion
    }
}

