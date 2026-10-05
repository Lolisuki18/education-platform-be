using System.Text;
using Domain.Exceptions;

namespace Domain.IdentityManagement.ValueObject
{
    public sealed class Password
    {
        #region Attributes
        public const int MinLength = 8;

        /// <summary>BCrypt only looks at the first 72 bytes, so anything longer would be silently ignored.</summary>
        public const int MaxBytes = 72;
        #endregion

        #region Properties
        public string Hash { get; }
        #endregion

        private Password(string hash)
        {
            Hash = hash;
        }

        #region Methods
        public static Password Create(string plainText)
        {
            Validate(plainText);

            var hash = BCrypt.Net.BCrypt.HashPassword(plainText);
            return new Password(hash);
        }

        /// <summary>Throws a <see cref="DomainException"/> describing the first rule the password breaks.</summary>
        public static void Validate(string? plainText)
        {
            if (string.IsNullOrWhiteSpace(plainText))
                throw new DomainException(
                    "Password cannot be empty");

            if (plainText.Length < MinLength)
                throw new DomainException(
                    $"Password must be at least {MinLength} characters");

            if (Encoding.UTF8.GetByteCount(plainText) > MaxBytes)
                throw new DomainException(
                    $"Password must not exceed {MaxBytes} bytes");

            if (!plainText.Any(char.IsLetter) || !plainText.Any(char.IsDigit))
                throw new DomainException(
                    "Password must contain at least one letter and one digit");
        }

        public bool Verify(string plainText)
        {
            return BCrypt.Net.BCrypt.Verify(plainText, Hash);
        }

        public override bool Equals(object? obj)
        {
            return obj is Password other && Hash == other.Hash;
        }

        public override int GetHashCode()
        {
            return Hash.GetHashCode();
        }
        #endregion
    }
}
