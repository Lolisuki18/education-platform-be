using Domain.DomainExceptions;
using Domain.IdentityManagement.ValueObject;
using Domain.IdentityManagement.Enum;

namespace Domain.IdentityManagement.Aggregate
{
    public class User
    {
        #region Attributes
        #endregion

        #region Properties
        public Guid UserID { get; private set; }
        public string Email { get; private set; }
        public Password Password { get; private set; }
        public RefreshToken? RefreshToken { get; private set; }
        public string Phone { get; private set; }
        public string Name { get; private set; }
        public string? Bio { get; private set; }
        public Role Role { get; private set; }
        public bool IsVerified { get; private set; }
        public string? EmailOtp { get; private set; }
        public DateTime? EmailOtpExpiresAt { get; private set; }
        public bool IsActive { get; private set; }
        public DateTime CreatedAt { get; private set; }
        #endregion

        protected User() { }

        public User(
            Guid userId,
            string email,
            string plainPassword,
            string phone,
            string name,
            string? bio,
            Role role,
            DateTime? createdAt,
            bool isVerified = false)
        {
            if (userId == Guid.Empty)
                throw new DomainException(
                    "User ID cannot be empty");

            if (string.IsNullOrWhiteSpace(email))
                throw new DomainException(
                    "Email is required");

            if (string.IsNullOrWhiteSpace(phone))
                throw new DomainException(
                    "Phone is required");

            if (!email.Contains("@"))
                throw new DomainException(
                    "Invalid email format");

            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException(
                    "Name is required");

            if (!System.Enum.IsDefined(typeof(Role), role))
                throw new DomainException(
                    "Invalid role");

            UserID = userId;
            Email = email;
            Password = Password.Create(plainPassword);
            Phone = phone;
            Name = name;
            Bio = bio;
            Role = role;
            IsVerified = isVerified;
            IsActive = true;
            CreatedAt = createdAt ?? DateTime.UtcNow;
        }



        #region Methods
        public void GenerateEmailOtp(TimeSpan lifetime)
        {
            EmailOtp = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 999999).ToString();
            EmailOtpExpiresAt = DateTime.UtcNow.Add(lifetime);
        }

        public void VerifyEmail(string otp)
        {
            if (IsVerified)
                throw new DomainException("Email already verified.");

            if (EmailOtp == null || EmailOtpExpiresAt == null)
                throw new DomainException("OTP not generated.");

            if (DateTime.UtcNow > EmailOtpExpiresAt)
                throw new DomainException("OTP has expired.");

            if (EmailOtp != otp)
                throw new DomainException("Invalid OTP.");

            IsVerified = true;
            EmailOtp = null;
            EmailOtpExpiresAt = null;
        }

        public bool VerifyLogin(string plainPassword)
        {
            return IsVerified && Password.Verify(plainPassword);
        }

        public void IssueRefreshToken(string rawToken, TimeSpan lifetime)
        {
            RefreshToken = RefreshToken.Create(rawToken, lifetime);
        }

        public bool CanRefresh(string rawToken)
        {
            return RefreshToken != null && RefreshToken.Verify(rawToken);
        }

        public void RevokeRefreshToken()
        {
            RefreshToken = null;
        }

        public void UpdateProfile(string? name, string? phone, string? bio)
        {
            Name = string.IsNullOrWhiteSpace(name) ? Name : name;
            Phone = string.IsNullOrWhiteSpace(phone) ? Phone : phone;
            Bio = string.IsNullOrWhiteSpace(bio) ? Bio : bio;
        }

        public void Activate()
        {
            IsActive = true;
        }

        public void Deactivate()
        {
            IsActive = false;
        }

        public void ChangeRole(Role role)
        {
            if (!System.Enum.IsDefined(typeof(Role), role))
                throw new DomainException("Invalid role");
            if (Role == role)
                throw new DomainException("User already has this role");
            Role = role;
        }
        #endregion
    }
}
