using System;
using System.Threading;
using System.Threading.Tasks;
using Application.BusinessException;
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

        public UpdateUserRoleCommandHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
        }

        public async Task Handle(UpdateUserRoleCommand request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            if (request.UserId == _currentUser.Id.Value)
                throw new BadRequest("Cannot change your own role.");

            var userRepo = _unitOfWork.GetRepository<IUserRepository>();
            var user = await userRepo.GetByIdAsync(request.UserId);

            if (user == null)
                throw new NotFound("User not found.");

            user.ChangeRole(request.Role);

            await _unitOfWork.CommitAsync(_currentUser.Id.Value.ToString());
        }
    }
}
