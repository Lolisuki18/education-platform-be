using Domain.DomainExceptions;

namespace Domain.IdentityManagement.ValueObject
{
    public sealed class Password
    {
        #region Attributes
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
            if (string.IsNullOrWhiteSpace(plainText))
                throw new DomainException(
                    "Password cannot be empty");

            if (plainText.Length < 8)
                throw new DomainException(
                    "Password must be at least 8 characters");

            var hash = BCrypt.Net.BCrypt.HashPassword(plainText);
            return new Password(hash);
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

