using Application.Exceptions;
using Application.Interface;
using Domain.Common.Interfaces;
using Domain.IdentityManagement.Aggregate;
using FluentValidation;
using MediatR;

namespace Application.Features.Users.Commands
{
    /// <summary>An administrator erases another person's account (for example after an erasure request by e-mail).</summary>
    public class DeleteUserCommand : IRequest
    {
        public Guid UserId { get; set; }
    }

    public class DeleteUserCommandValidator : AbstractValidator<DeleteUserCommand>
    {
        public DeleteUserCommandValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("UserId is required.");
        }
    }

    public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;
        private readonly IUserActivityCache _activityCache;
        private readonly TimeProvider _timeProvider;

        public DeleteUserCommandHandler(
            IUnitOfWork unitOfWork,
            ICurrentUser currentUser,
            IUserActivityCache activityCache,
            TimeProvider timeProvider)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _activityCache = activityCache;
            _timeProvider = timeProvider;
        }

        public async Task Handle(DeleteUserCommand request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            if (request.UserId == _currentUser.Id.Value)
                throw new BadRequestException("Use the account deletion of your own profile to delete yourself.");

            var user = await _unitOfWork.GetRepository<IUserRepository>()
                .GetByIdWithSessions(request.UserId, cancellationToken)
                ?? throw new NotFoundException("User not found.");

            await AccountEraser.EraseAsync(
                _unitOfWork, _activityCache, _timeProvider, user, _currentUser.Id.Value.ToString(), cancellationToken);
        }
    }
}
