using MediatR;
using Domain.Common.Interfaces;
using Application.BusinessException;

namespace Application.Features.Identity.Commands.VerifyEmail
{
    public class VerifyEmailCommand : IRequest<Unit>
    {
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
            // Validate user existence
            var user = await _unitOfWork
                .GetRepository<IUserRepository>()
                .GetUserByOTP(request.Otp);

            if (user == null)
                throw new NotFound("User not found.");

            // Apply domain
            user.VerifyEmail(request.Otp);

            // Apply persistence
            await _unitOfWork.BeginTransactionAsync();
            await _unitOfWork.CommitAsync();

            return Unit.Value;
        }
    }
}
