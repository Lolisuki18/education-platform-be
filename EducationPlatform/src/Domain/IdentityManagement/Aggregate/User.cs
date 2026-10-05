using Domain.Exceptions;
using Domain.IdentityManagement.Entity;
using Domain.IdentityManagement.ValueObject;
using Domain.IdentityManagement.Enum;

namespace Domain.IdentityManagement.Aggregate
{
    public class User
    {
        #region Attributes
        public const int MaxActiveSessions = 10;

        private readonly List<RefreshSession> _refreshSessions = new();
        #endregion

        #region Properties
        public Guid UserID { get; private set; }
        public string Email { get; private set; }
        public Password Password { get; private set; }
        public string Phone { get; private set; }
        public string Name { get; private set; }
        public string? Bio { get; private set; }
        public Role Role { get; private set; }
        public bool IsVerified { get; private set; }
        public string? EmailOtp { get; private set; }
        public DateTime? EmailOtpExpiresAt { get; private set; }
        public bool IsActive { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public IReadOnlyCollection<RefreshSession> RefreshSessions => _refreshSessions.AsReadOnly();
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
        /// <summary>
        /// Creates a new one-time code and returns it so it can be e-mailed. Only a hash is stored, so a
        /// leaked database does not reveal codes that are still valid.
        /// </summary>
        public string GenerateEmailOtp(TimeSpan lifetime)
        {
            var otp = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 999999).ToString();

            EmailOtp = HashOtp(otp);
            EmailOtpExpiresAt = DateTime.UtcNow.Add(lifetime);

            return otp;
        }

        /// <summary>
        /// False while the last code is still younger than <paramref name="cooldown"/>, so a client cannot
        /// make the platform send an unlimited number of e-mails to one address.
        /// </summary>
        public bool CanRequestNewOtp(TimeSpan lifetime, TimeSpan cooldown)
        {
            if (EmailOtpExpiresAt == null)
                return true;

            var generatedAt = EmailOtpExpiresAt.Value - lifetime;
            return DateTime.UtcNow - generatedAt >= cooldown;
        }

        public void VerifyEmail(string otp)
        {
            if (IsVerified)
                throw new DomainException("Email already verified.");

            if (EmailOtp == null || EmailOtpExpiresAt == null)
                throw new DomainException("OTP not generated.");

            if (DateTime.UtcNow > EmailOtpExpiresAt)
                throw new DomainException("OTP has expired.");

            var expected = System.Text.Encoding.UTF8.GetBytes(EmailOtp);
            var actual = System.Text.Encoding.UTF8.GetBytes(HashOtp(otp ?? string.Empty));
            if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expected, actual))
                throw new DomainException("Invalid OTP.");

            IsVerified = true;
            EmailOtp = null;
            EmailOtpExpiresAt = null;
        }

        /// <summary>
        /// Somebody registers again with an e-mail address that was never verified. The new details replace the
        /// old ones: otherwise whoever registered first (not necessarily the owner of the mailbox) would keep
        /// control of the password once the real owner verifies the address.
        /// </summary>
        public void ReissueRegistration(string plainPassword, string phone, string name, string? bio, Role role)
        {
            if (IsVerified)
                throw new DomainException("Email already verified.");

            if (string.IsNullOrWhiteSpace(phone))
                throw new DomainException("Phone is required");

            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException("Name is required");

            if (!System.Enum.IsDefined(typeof(Role), role))
                throw new DomainException("Invalid role");

            Password = Password.Create(plainPassword);
            Phone = phone;
            Name = name;
            Bio = bio;
            Role = role;
        }

        private string HashOtp(string otp)
        {
            // Bound to the user so identical codes of different users do not share a hash
            var bytes = System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes($"{UserID:N}:{otp}"));
            return Convert.ToHexString(bytes);
        }

        public bool VerifyLogin(string plainPassword)
        {
            return IsVerified && Password.Verify(plainPassword);
        }

        /// <summary>Starts a new session (one per device) and returns it.</summary>
        public RefreshSession IssueRefreshToken(string rawToken, TimeSpan lifetime)
        {
            var session = new RefreshSession(UserID, rawToken, lifetime);

            // Drop sessions that can no longer be used, then cap the number of devices.
            _refreshSessions.RemoveAll(s => s.IsExpired);

            var active = _refreshSessions.Where(s => s.IsActive).OrderBy(s => s.CreatedAt).ToList();
            for (var i = 0; i <= active.Count - MaxActiveSessions; i++)
                active[i].Revoke();

            _refreshSessions.Add(session);
            return session;
        }

        public bool CanRefresh(string rawToken)
        {
            return _refreshSessions.Any(s => s.IsActive && s.Matches(rawToken));
        }

        /// <summary>
        /// Rotates the session that owns <paramref name="oldToken"/>. If the token was already rotated
        /// (a replay) every session of the user is revoked, since the token family may be stolen.
        /// </summary>
        public RefreshResult RotateRefreshToken(string oldToken, string newToken, TimeSpan lifetime)
        {
            var session = _refreshSessions.FirstOrDefault(s => s.Matches(oldToken));
            if (session == null)
                return RefreshResult.Invalid;

            if (session.IsRevoked)
            {
                if (!session.IsReplay)
                    return RefreshResult.Invalid;

                RevokeAllRefreshTokens();
                return RefreshResult.ReuseDetected;
            }

            if (session.IsExpired)
                return RefreshResult.Expired;

            session.Revoke();
            IssueRefreshToken(newToken, lifetime);
            return RefreshResult.Rotated;
        }

        /// <summary>Revokes the session of a single device. Returns false when no such session exists.</summary>
        public bool RevokeRefreshToken(string rawToken)
        {
            var session = _refreshSessions.FirstOrDefault(s => s.Matches(rawToken));
            if (session == null)
                return false;

            session.Revoke();
            return true;
        }

        public void RevokeAllRefreshTokens()
        {
            foreach (var session in _refreshSessions)
                session.Revoke();
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
