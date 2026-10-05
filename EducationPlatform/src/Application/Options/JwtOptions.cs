using System.Text;
using Microsoft.Extensions.Options;

namespace Application.Options
{
    public class JwtOptions
    {
        public const string SectionName = "JwtSettings";

        public string SecretKey { get; set; } = string.Empty;

        public string Issuer { get; set; } = string.Empty;

        public string Audience { get; set; } = string.Empty;

        /// <summary>Lifetime of an access token. Fractions are allowed (0.5 = 30 seconds).</summary>
        public double ExpiryMinutes { get; set; } = 60;

        public IEnumerable<string> Validate()
        {
            if (string.IsNullOrWhiteSpace(SecretKey))
                yield return "Missing configuration: JwtSettings:SecretKey";
            else if (Encoding.UTF8.GetByteCount(SecretKey) < 32)
                yield return "JwtSettings:SecretKey must be at least 32 bytes (256 bits) long for HS256 signing.";

            if (string.IsNullOrWhiteSpace(Issuer))
                yield return "Missing configuration: JwtSettings:Issuer";

            if (string.IsNullOrWhiteSpace(Audience))
                yield return "Missing configuration: JwtSettings:Audience";

            if (ExpiryMinutes <= 0)
                yield return "JwtSettings:ExpiryMinutes must be greater than zero.";
        }
    }

    public class JwtOptionsValidator : IValidateOptions<JwtOptions>
    {
        public ValidateOptionsResult Validate(string? name, JwtOptions options)
        {
            var failures = options.Validate().ToList();
            return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
        }
    }
}
