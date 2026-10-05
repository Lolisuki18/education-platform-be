using MediatR;
using Domain.Common.Interfaces;
using Application.Exceptions;
using Domain.IdentityManagement.Aggregate;
using Application.Interface;
using Domain.Exceptions;

namespace Application.Features.Identity.Commands.VerifyEmail
{
    public class VerifyEmailCommand : IRequest<Unit>
    {
        public string Email { get; set; } = string.Empty;
        public string Otp { get; set; } = string.Empty;
    }

    public class VerifyEmailCommandHandler : IRequestHandler<VerifyEmailCommand, Unit>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILoginAttemptTracker _attemptTracker;

        public VerifyEmailCommandHandler(IUnitOfWork unitOfWork, ILoginAttemptTracker attemptTracker)
        {
            _unitOfWork = unitOfWork;
            _attemptTracker = attemptTracker;
        }

        public async Task<Unit> Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
        {
            // A 6-digit OTP can be brute-forced, so wrong guesses are limited per account
            var attemptKey = $"otp:{request.Email}";
            if (_attemptTracker.IsLockedOut(attemptKey))
                throw new TooManyRequestsException("Too many invalid verification codes. Please try again later.");

            // Validate user existence by email (instead of globally by OTP)
            var user = await _unitOfWork
                .GetRepository<IUserRepository>()
                .GetUserByEmail(request.Email, cancellationToken);

            if (user == null)
                throw new NotFoundException("User not found.");

            // Apply domain logic (compares request.Otp with user's stored EmailOtp)
            try
            {
                user.VerifyEmail(request.Otp);
            }
            catch (DomainException)
            {
                _attemptTracker.RegisterFailure(attemptKey);
                throw;
            }

            _attemptTracker.Reset(attemptKey);

            // Apply persistence
            await _unitOfWork.BeginTransactionAsync();
            await _unitOfWork.CommitAsync();

            return Unit.Value;
        }
    }
}
