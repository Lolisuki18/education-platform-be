using System.Text;
using Domain.IdentityManagement.ValueObject;
using FluentValidation;

namespace Application.Features.Identity.Commands.Register
{
    public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
    {
        public RegisterCommandValidator()
        {
            RuleFor(v => v.Email)
                .NotEmpty().WithMessage("Email is required.")
                .EmailAddress().WithMessage("Invalid email format.")
                .MaximumLength(200).WithMessage("Email must not exceed 200 characters.");

            RuleFor(v => v.Password)
                .NotEmpty().WithMessage("Password is required.")
                .MinimumLength(Password.MinLength).WithMessage($"Password must be at least {Password.MinLength} characters.")
                .Must(p => p == null || Encoding.UTF8.GetByteCount(p) <= Password.MaxBytes)
                    .WithMessage($"Password must not exceed {Password.MaxBytes} bytes.")
                .Must(p => p != null && p.Any(char.IsLetter) && p.Any(char.IsDigit))
                    .WithMessage("Password must contain at least one letter and one digit.");

            RuleFor(v => v.Phone)
                .NotEmpty().WithMessage("Phone is required.")
                .Matches(@"^\d{10,11}$").WithMessage("Phone must be 10-11 digits.");

            RuleFor(v => v.Name)
                .NotEmpty().WithMessage("Name is required.")
                .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");
        }
    }
}
