using MediatR;
using Domain.Common.Interfaces;
using Domain.IdentityManagement.Aggregate;

namespace Application.Features.Identity.Commands.Logout
{
    public class LogoutCommand : IRequest<Unit>
    {
        public Guid UserId { get; set; }

        /// <summary>When set only that device's session is revoked, otherwise every session is.</summary>
        public string? RefreshToken { get; set; }
    }

    public class LogoutCommandHandler : IRequestHandler<LogoutCommand, Unit>
    {
        private readonly IUnitOfWork _unitOfWork;

        public LogoutCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Unit> Handle(LogoutCommand request, CancellationToken cancellationToken)
        {
            var user = await _unitOfWork.GetRepository<IUserRepository>().GetByIdWithSessions(request.UserId, cancellationToken);
            if (user != null)
            {
                if (string.IsNullOrWhiteSpace(request.RefreshToken))
                    user.RevokeAllRefreshTokens();
                else
                    user.RevokeRefreshToken(request.RefreshToken);

                await _unitOfWork.BeginTransactionAsync();
                await _unitOfWork.CommitAsync();
            }
            return Unit.Value;
        }
    }
}
