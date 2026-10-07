using Application.Interface;
using Domain.Common.Interfaces;
using Domain.IdentityManagement.Aggregate;
using FluentValidation;
using MediatR;

namespace Application.Features.Identity.Commands.ForgotPassword
{
    /// <summary>Sends a one-time code to the e-mail address of the account, when there is such an account.</summary>
    public class ForgotPasswordCommand : IRequest<Unit>
    {
        public string Email { get; set; } = string.Empty;
    }

    public class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
    {
        public ForgotPasswordCommandValidator()
        {
            RuleFor(v => v.Email)
                .NotEmpty().WithMessage("Email is required.")
                .EmailAddress().WithMessage("Invalid email format.")
                .MaximumLength(200).WithMessage("Email must not exceed 200 characters.");
        }
    }

    public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, Unit>
    {
        public static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(10);

        /// <summary>Minimum time between two reset e-mails for the same address.</summary>
        public static readonly TimeSpan OtpResendCooldown = TimeSpan.FromSeconds(60);

        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailService _emailService;

        public ForgotPasswordCommandHandler(IUnitOfWork unitOfWork, IEmailService emailService)
        {
            _unitOfWork = unitOfWork;
            _emailService = emailService;
        }

        public async Task<Unit> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
        {
            var user = await _unitOfWork.GetRepository<IUserRepository>()
                .GetUserByEmail(request.Email, cancellationToken);

            // The answer is the same whether or not the address belongs to an account (or may reset): otherwise
            // this endpoint would tell anybody which e-mail addresses are registered
            if (user == null || !user.CanResetPassword || !user.CanRequestPasswordReset(OtpLifetime, OtpResendCooldown))
                return Unit.Value;

            var otp = user.GeneratePasswordResetOtp(OtpLifetime);

            await _unitOfWork.BeginTransactionAsync();
            await _unitOfWork.CommitAsync();

            // Only the e-mail contains the code: the database keeps just a hash of it
            await _emailService.SendPasswordResetEmailAsync(user.Email, otp);

            return Unit.Value;
        }
    }
}
