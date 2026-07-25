using MediatR;
using Domain.Common.Interfaces;
using Application.BusinessException;
using Domain.IdentityManagement.Aggregate;

namespace Application.Features.Identity.Commands.Logout
{
    public class LogoutCommand : IRequest<Unit>
    {
        public Guid UserId { get; set; }
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
            var user = await _unitOfWork.GetRepository<IUserRepository>().GetByIdAsync(request.UserId, cancellationToken);
            if (user != null)
            {
                user.RevokeRefreshToken();
                await _unitOfWork.BeginTransactionAsync();
                await _unitOfWork.GetRepository<IUserRepository>().UpdateAsync(user.UserID, user, cancellationToken);
                await _unitOfWork.CommitAsync();
            }
            return Unit.Value;
        }
    }
}
