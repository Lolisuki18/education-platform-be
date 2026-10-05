using Application.Exceptions;
using Application.Interface;
using Domain.Common.Interfaces;
using Domain.IdentityManagement.Aggregate;
using FluentValidation;
using MediatR;

namespace Application.Features.Users.Commands
{
    /// <summary>People delete their own account. The password is asked again so a stolen access token is not enough.</summary>
    public class DeleteMyAccountCommand : IRequest
    {
        public string Password { get; set; } = string.Empty;
    }

    public class DeleteMyAccountCommandValidator : AbstractValidator<DeleteMyAccountCommand>
    {
        public DeleteMyAccountCommandValidator()
        {
            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Password is required.");
        }
    }

    public class DeleteMyAccountCommandHandler : IRequestHandler<DeleteMyAccountCommand>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;
        private readonly IUserActivityCache _activityCache;
        private readonly TimeProvider _timeProvider;

        public DeleteMyAccountCommandHandler(
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

        public async Task Handle(DeleteMyAccountCommand request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            var user = await _unitOfWork.GetRepository<IUserRepository>()
                .GetByIdWithSessions(_currentUser.Id.Value, cancellationToken)
                ?? throw new NotFoundException("User not found.");

            if (!user.Password.Verify(request.Password))
                throw new BadRequestException("The password is incorrect.");

            await AccountEraser.EraseAsync(
                _unitOfWork, _activityCache, _timeProvider, user, _currentUser.Id.Value.ToString(), cancellationToken);
        }
    }
}
