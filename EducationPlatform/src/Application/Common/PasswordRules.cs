using System.Text;
using Domain.IdentityManagement.ValueObject;
using FluentValidation;

namespace Application.Common
{
    public static class PasswordRules
    {
        /// <summary>The same password policy the domain enforces, reported as validation errors before any work is done.</summary>
        public static IRuleBuilderOptions<T, string> MeetsPasswordPolicy<T>(this IRuleBuilder<T, string> rule)
        {
            return rule
                .NotEmpty().WithMessage("Password is required.")
                .MinimumLength(Password.MinLength).WithMessage($"Password must be at least {Password.MinLength} characters.")
                .Must(p => p == null || Encoding.UTF8.GetByteCount(p) <= Password.MaxBytes)
                    .WithMessage($"Password must not exceed {Password.MaxBytes} bytes.")
                .Must(p => p != null && p.Any(char.IsLetter) && p.Any(char.IsDigit))
                    .WithMessage("Password must contain at least one letter and one digit.");
        }
    }
}
