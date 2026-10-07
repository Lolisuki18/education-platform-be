using Application.Common;
using Application.Exceptions;
using Application.Interface;
using Domain.Common.Interfaces;
using Domain.Exceptions;
using Domain.IdentityManagement.Aggregate;
using FluentValidation;
using MediatR;

namespace Application.Features.Identity.Commands.ResetPassword
{
    /// <summary>Sets a new password using the code that was e-mailed by "forgot password".</summary>
    public class ResetPasswordCommand : IRequest<Unit>
    {
        public string Email { get; set; } = string.Empty;
        public string Otp { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
    {
        public ResetPasswordCommandValidator()
        {
            RuleFor(v => v.Email)
                .NotEmpty().WithMessage("Email is required.")
                .EmailAddress().WithMessage("Invalid email format.");

            RuleFor(v => v.Otp)
                .NotEmpty().WithMessage("Code is required.")
                .Matches(@"^\d{6}$").WithMessage("The code has 6 digits.");

            RuleFor(v => v.NewPassword).MeetsPasswordPolicy();
        }
    }

    public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, Unit>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILoginAttemptTracker _attemptTracker;
        private readonly IEmailService _emailService;

        public ResetPasswordCommandHandler(IUnitOfWork unitOfWork, ILoginAttemptTracker attemptTracker, IEmailService emailService)
        {
            _unitOfWork = unitOfWork;
            _attemptTracker = attemptTracker;
            _emailService = emailService;
        }

        public async Task<Unit> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
        {
            // A 6-digit code can be brute-forced, so wrong guesses are limited per address (also for unknown ones,
            // which keeps this endpoint from revealing which addresses exist)
            var attemptKey = $"reset:{request.Email}";
            if (_attemptTracker.IsLockedOut(attemptKey))
                throw new TooManyRequestsException("Too many invalid codes. Please try again later.");

            var user = await _unitOfWork.GetRepository<IUserRepository>()
                .GetUserByEmail(request.Email, cancellationToken);

            if (user == null)
            {
                _attemptTracker.RegisterFailure(attemptKey);
                throw new BadRequestException("Invalid or expired code.");
            }

            try
            {
                user.ResetPassword(request.Otp, request.NewPassword);
            }
            catch (DomainException)
            {
                _attemptTracker.RegisterFailure(attemptKey);
                throw;
            }

            await _unitOfWork.BeginTransactionAsync();
            await _unitOfWork.CommitAsync();

            _attemptTracker.Reset(attemptKey);

            // Failed sign-ins before the reset must not keep a person who just proved ownership of the mailbox locked out
            _attemptTracker.Reset(request.Email);

            // The owner of the mailbox learns about it, which matters most when the reset was not theirs
            await _emailService.SendPasswordChangedEmailAsync(user.Email);

            return Unit.Value;
        }
    }
}
