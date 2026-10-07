using Application.Common;
using Application.Exceptions;
using Application.Interface;
using Application.Results;
using Domain.Common.Interfaces;
using Domain.IdentityManagement.Aggregate;
using FluentValidation;
using MediatR;

namespace Application.Features.Identity.Commands.ChangePassword
{
    /// <summary>A signed-in user changes the password. Every other device is signed out; this one gets fresh tokens.</summary>
    public class ChangePasswordCommand : IRequest<TokenDTO>
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
    {
        public ChangePasswordCommandValidator()
        {
            RuleFor(v => v.CurrentPassword)
                .NotEmpty().WithMessage("The current password is required.");

            RuleFor(v => v.NewPassword).MeetsPasswordPolicy();

            RuleFor(v => v.NewPassword)
                .NotEqual(v => v.CurrentPassword).WithMessage("The new password must be different from the current one.");
        }
    }

    public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, TokenDTO>
    {
        private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;
        private readonly ITokenService _tokenService;
        private readonly ILoginAttemptTracker _attemptTracker;

        public ChangePasswordCommandHandler(
            IUnitOfWork unitOfWork,
            ICurrentUser currentUser,
            ITokenService tokenService,
            ILoginAttemptTracker attemptTracker)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _tokenService = tokenService;
            _attemptTracker = attemptTracker;
        }

        public async Task<TokenDTO> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            // Somebody holding a stolen access token must not be able to guess the current password
            var attemptKey = $"password:{_currentUser.Id.Value}";
            if (_attemptTracker.IsLockedOut(attemptKey))
                throw new TooManyRequestsException("Too many failed attempts. Please try again later.");

            var user = await _unitOfWork.GetRepository<IUserRepository>()
                .GetByIdWithSessions(_currentUser.Id.Value, cancellationToken)
                ?? throw new NotFoundException("User not found.");

            if (!user.Password.Verify(request.CurrentPassword))
            {
                _attemptTracker.RegisterFailure(attemptKey);
                throw new BadRequestException("The current password is incorrect.");
            }

            _attemptTracker.Reset(attemptKey);

            user.ChangePassword(request.CurrentPassword, request.NewPassword);

            // All sessions were revoked above; this device continues with a new one
            var refreshToken = _tokenService.GenerateRefreshToken();
            user.IssueRefreshToken(refreshToken, RefreshTokenLifetime);

            await _unitOfWork.BeginTransactionAsync();
            await _unitOfWork.CommitAsync(user.UserID.ToString());

            return new TokenDTO
            {
                Token = _tokenService.GenerateToken(user),
                RefreshToken = refreshToken
            };
        }
    }
}
