using MediatR;
using Domain.Common.Interfaces;
using Application.BusinessException;
using Domain.IdentityManagement.Aggregate;

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

        public VerifyEmailCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Unit> Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
        {
            // Validate user existence by email (instead of globally by OTP)
            var user = await _unitOfWork
                .GetRepository<IUserRepository>()
                .GetUserByEmail(request.Email);

            if (user == null)
                throw new NotFound("User not found.");

            // Apply domain logic (compares request.Otp with user's stored EmailOtp)
            user.VerifyEmail(request.Otp);

            // Apply persistence
            await _unitOfWork.BeginTransactionAsync();
            await _unitOfWork.CommitAsync();

            return Unit.Value;
        }
    }
}
