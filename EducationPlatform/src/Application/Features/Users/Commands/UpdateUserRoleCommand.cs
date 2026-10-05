using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Exceptions;
using Application.Interface;
using Domain.Common.Interfaces;
using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.Enum;
using MediatR;

namespace Application.Features.Users.Commands
{
    public class UpdateUserRoleCommand : IRequest
    {
        public Guid UserId { get; set; }
        public Role Role { get; set; }
    }

    public class UpdateUserRoleCommandHandler : IRequestHandler<UpdateUserRoleCommand>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;
        private readonly IUserActivityCache _activityCache;

        public UpdateUserRoleCommandHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser, IUserActivityCache activityCache)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _activityCache = activityCache;
        }

        public async Task Handle(UpdateUserRoleCommand request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            if (request.UserId == _currentUser.Id.Value)
                throw new BadRequestException("Cannot change your own role.");

            var userRepo = _unitOfWork.GetRepository<IUserRepository>();
            var user = await userRepo.GetByIdAsync(request.UserId, cancellationToken);

            if (user == null)
                throw new NotFoundException("User not found.");

            user.ChangeRole(request.Role);

            await _unitOfWork.CommitAsync(_currentUser.Id.Value.ToString());

            // The user's current access token still carries the old role; dropping the cached status makes the
            // API notice the mismatch on their next request and ask the client to refresh its session.
            _activityCache.Invalidate(request.UserId);
        }
    }
}
