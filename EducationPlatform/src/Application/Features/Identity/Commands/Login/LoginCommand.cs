using MediatR;
using Application.Results;
using Domain.Common.Interfaces;
using Application.Exceptions;
using Domain.IdentityManagement.Aggregate;
using Application.Interface;

namespace Application.Features.Identity.Commands.Login
{
    public class LoginCommand : IRequest<TokenDTO>
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginCommandHandler : IRequestHandler<LoginCommand, TokenDTO>
    {
        private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

        private readonly ITokenService _tokenService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILoginAttemptTracker _attemptTracker;

        public LoginCommandHandler(
            IUnitOfWork unitOfWork,
            ITokenService tokenService,
            ILoginAttemptTracker attemptTracker)
        {
            _unitOfWork = unitOfWork;
            _tokenService = tokenService;
            _attemptTracker = attemptTracker;
        }

        public async Task<TokenDTO> Handle(LoginCommand request, CancellationToken cancellationToken)
        {
            if (_attemptTracker.IsLockedOut(request.Email))
                throw new TooManyRequestsException("Too many failed login attempts. Please try again later.");

            // Validate user existence
            var user = await _unitOfWork
                .GetRepository<IUserRepository>()
                .GetUserByEmail(request.Email, cancellationToken);

            // Validate password and email verification
            if (user == null || !user.VerifyLogin(request.Password))
            {
                _attemptTracker.RegisterFailure(request.Email);
                throw new AuthenticateException("Invalid credentials.");
            }

            _attemptTracker.Reset(request.Email);

            // Generate token
            var token = _tokenService.GenerateToken(user);

            // Generate refresh token
            var refreshToken = _tokenService.GenerateRefreshToken();

            // Apply domain: every login starts its own session, other devices stay signed in
            user.IssueRefreshToken(refreshToken, RefreshTokenLifetime);

            // Apply persistence
            await _unitOfWork.BeginTransactionAsync();
            await _unitOfWork.CommitAsync();

            return new TokenDTO
            {
                Token = token,
                RefreshToken = refreshToken
            };
        }
    }
}
