using System;
using System.Threading;
using System.Threading.Tasks;
using Application.BusinessException;
using Application.Interface;
using Domain.Common.Interfaces;
using Domain.IdentityManagement.Aggregate;
using MediatR;
using Microsoft.Extensions.Caching.Memory;

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
        private readonly IMemoryCache _memoryCache;

        public UpdateUserStatusCommandHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser, IMemoryCache memoryCache)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _memoryCache = memoryCache;
        }

        public async Task Handle(UpdateUserStatusCommand request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            if (request.UserId == _currentUser.Id.Value)
                throw new BadRequest("Cannot deactivate your own account.");

            var userRepo = _unitOfWork.GetRepository<IUserRepository>();
            var user = await userRepo.GetByIdAsync(request.UserId);

            if (user == null)
                throw new NotFound("User not found.");

            if (request.IsActive)
            {
                user.Activate();
            }
            else
            {
                user.Deactivate();
            }

            // Invalidate the active status cache key
            _memoryCache.Remove($"UserActive:{request.UserId}");

            await _unitOfWork.CommitAsync(_currentUser.Id.Value.ToString());
        }
    }
}
