using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Exceptions;
using Application.Interface;
using Domain.Common.Interfaces;
using Domain.IdentityManagement.Aggregate;
using MediatR;

namespace Application.Features.Users.Commands
{
    public class UpdateUserStatusCommand : IRequest
    {
        public Guid UserId { get; set; }
        public bool IsActive { get; set; }
    }

    public class UpdateUserStatusCommandHandler : IRequestHandler<UpdateUserStatusCommand>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;
        private readonly IUserActivityCache _activityCache;

        public UpdateUserStatusCommandHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser, IUserActivityCache activityCache)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _activityCache = activityCache;
        }

        public async Task Handle(UpdateUserStatusCommand request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            if (request.UserId == _currentUser.Id.Value)
                throw new BadRequestException("Cannot deactivate your own account.");

            var userRepo = _unitOfWork.GetRepository<IUserRepository>();
            var user = await userRepo.GetByIdAsync(request.UserId, cancellationToken);

            if (user == null)
                throw new NotFoundException("User not found.");

            if (request.IsActive)
            {
                user.Activate();
            }
            else
            {
                user.Deactivate();
            }

            await _unitOfWork.CommitAsync(_currentUser.Id.Value.ToString());

            // Only after the commit: invalidating earlier lets a concurrent request re-cache the old status
            _activityCache.Invalidate(request.UserId);
        }
    }
}
