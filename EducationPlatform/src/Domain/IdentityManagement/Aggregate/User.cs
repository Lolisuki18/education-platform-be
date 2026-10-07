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
        public const string DeletedName = "Deleted user";

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
        public string? PasswordResetOtp { get; private set; }
        public DateTime? PasswordResetOtpExpiresAt { get; private set; }
        public bool IsActive { get; private set; }
        public DateTime CreatedAt { get; private set; }
        /// <summary>When the account was erased. The row stays (orders and enrollments point to it) but holds no personal data.</summary>
        public DateTime? DeletedAt { get; private set; }
        /// <summary>Access tokens issued before this moment are no longer accepted (set whenever all sessions are revoked).</summary>
        public DateTime? TokensValidFrom { get; private set; }
        public bool IsDeleted => DeletedAt.HasValue;
        public IReadOnlyCollection<RefreshSession> RefreshSessions => _refreshSessions.AsReadOnly();
        #endregion

        // EF Core calls this constructor and then fills the properties
#pragma warning disable CS8618
        protected User() { }
#pragma warning restore CS8618

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
            Email = NormalizeEmail(email);
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
        /// The one form an address is stored and looked up in: an address typed as "Alice@Mail.com " (mobile keyboards
        /// capitalise the first letter) must reach the same account as "alice@mail.com".
        /// </summary>
        public static string NormalizeEmail(string email) => (email ?? string.Empty).Trim().ToLowerInvariant();

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

        /// <summary>Only verified, active and not erased accounts can recover their password by e-mail.</summary>
        public bool CanResetPassword => IsVerified && IsActive && !IsDeleted;

        /// <summary>
        /// Creates a one-time code for "forgot password" and returns it so it can be e-mailed. Like the
        /// verification code it is stored hashed, and it is a separate secret so one cannot be used for the other.
        /// </summary>
        public string GeneratePasswordResetOtp(TimeSpan lifetime)
        {
            if (!CanResetPassword)
                throw new DomainException("This account cannot reset its password.");

            var otp = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 999999).ToString();

            PasswordResetOtp = HashResetOtp(otp);
            PasswordResetOtpExpiresAt = DateTime.UtcNow.Add(lifetime);

            return otp;
        }

        /// <summary>False while the last reset code is still younger than <paramref name="cooldown"/>.</summary>
        public bool CanRequestPasswordReset(TimeSpan lifetime, TimeSpan cooldown)
        {
            if (PasswordResetOtpExpiresAt == null)
                return true;

            var generatedAt = PasswordResetOtpExpiresAt.Value - lifetime;
            return DateTime.UtcNow - generatedAt >= cooldown;
        }

        /// <summary>
        /// Sets a new password when <paramref name="otp"/> is the code that was e-mailed. Every session is
        /// revoked: whoever knew the old password (or stole a token) must sign in again.
        /// </summary>
        public void ResetPassword(string otp, string newPlainPassword)
        {
            // One message for every failure, so a caller cannot tell "no code was requested" from "wrong code"
            const string invalid = "Invalid or expired code.";

            if (!CanResetPassword || PasswordResetOtp == null || PasswordResetOtpExpiresAt == null)
                throw new DomainException(invalid);

            if (DateTime.UtcNow > PasswordResetOtpExpiresAt)
                throw new DomainException(invalid);

            var expected = System.Text.Encoding.UTF8.GetBytes(PasswordResetOtp);
            var actual = System.Text.Encoding.UTF8.GetBytes(HashResetOtp(otp ?? string.Empty));
            if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expected, actual))
                throw new DomainException(invalid);

            Password = Password.Create(newPlainPassword);
            PasswordResetOtp = null;
            PasswordResetOtpExpiresAt = null;
            RevokeAllRefreshTokens();
        }

        /// <summary>Changes the password of a signed-in user. Every session is revoked; the caller starts a new one.</summary>
        public void ChangePassword(string currentPlainPassword, string newPlainPassword)
        {
            if (!Password.Verify(currentPlainPassword))
                throw new DomainException("The current password is incorrect.");

            if (Password.Verify(newPlainPassword))
                throw new DomainException("The new password must be different from the current one.");

            Password = Password.Create(newPlainPassword);
            PasswordResetOtp = null;
            PasswordResetOtpExpiresAt = null;
            RevokeAllRefreshTokens();
        }

        private string HashResetOtp(string otp)
        {
            // A different purpose string, so a verification code never matches a reset code
            var bytes = System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes($"{UserID:N}:reset:{otp}"));
            return Convert.ToHexString(bytes);
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

        /// <summary>
        /// Ends every session. Refresh tokens are revoked, and access tokens issued until now stop working too,
        /// otherwise a stolen access token would outlive a password change or a "log out everywhere".
        /// </summary>
        public void RevokeAllRefreshTokens()
        {
            foreach (var session in _refreshSessions)
                session.Revoke();

            TokensValidFrom = DateTime.UtcNow;
        }

        public void UpdateProfile(string? name, string? phone, string? bio)
        {
            Name = string.IsNullOrWhiteSpace(name) ? Name : name;
            Phone = string.IsNullOrWhiteSpace(phone) ? Phone : phone;
            Bio = string.IsNullOrWhiteSpace(bio) ? Bio : bio;
        }

        public void Activate()
        {
            if (IsDeleted)
                throw new DomainException("A deleted account cannot be activated.");

            IsActive = true;
        }

        public void Deactivate()
        {
            IsActive = false;
        }

        /// <summary>
        /// Right to erasure: removes everything that identifies the person but keeps the row, because orders,
        /// enrollments and reviews must still point to a user. The e-mail address and phone number are released
        /// (the placeholders are unique per user), so the person can register again, and nobody can log in.
        /// </summary>
        public void Erase(DateTime now)
        {
            if (IsDeleted)
                throw new DomainException("This account is already deleted.");

            var token = UserID.ToString("N");

            Email = $"deleted-{token}@deleted.invalid";
            Phone = $"x{token[..18]}";
            Name = DeletedName;
            Bio = null;
            EmailOtp = null;
            EmailOtpExpiresAt = null;
            PasswordResetOtp = null;
            PasswordResetOtpExpiresAt = null;

            // Nobody knows this password, so the credentials of the old account are gone for good
            Password = Password.Create($"{Guid.NewGuid():N}Aa1");

            RevokeAllRefreshTokens();
            IsActive = false;
            DeletedAt = now;
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
