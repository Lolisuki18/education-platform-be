using MediatR;
using Application.Results;
using Domain.Common.Interfaces;
using Application.Common;
using Application.Exceptions;
using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.Enum;
using Application.Interface;

namespace Application.Features.Identity.Commands.RefreshToken
{
    public class RefreshTokenCommand : IRequest<TokenDTO>
    {
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, TokenDTO>
    {
        private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

        private readonly ITokenService _tokenService;
        private readonly IUnitOfWork _unitOfWork;

        public RefreshTokenCommandHandler(IUnitOfWork unitOfWork, ITokenService tokenService)
        {
            _unitOfWork = unitOfWork;
            _tokenService = tokenService;
        }

        public async Task<TokenDTO> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
        {
            // Find user by refresh token
            var user = await _unitOfWork
                .GetRepository<IUserRepository>()
                .GetByRefreshToken(request.RefreshToken, cancellationToken);

            if (user == null)
                throw new AuthenticateException("Invalid refresh token.");

            if (!user.IsActive)
                throw new ForbiddenException("Your account is inactive or has been locked.");

            // Generate the replacement first so the domain can rotate in a single step
            var newRefreshToken = _tokenService.GenerateRefreshToken();

            // Apply domain
            var outcome = user.RotateRefreshToken(request.RefreshToken, newRefreshToken, RefreshTokenLifetime);

            switch (outcome)
            {
                case RefreshResult.Rotated:
                    break;

                case RefreshResult.ReuseDetected:
                    PlatformMetrics.RefreshTokenReplays.Add(1);
                    // A rotated token was replayed: every session of the user has been revoked, persist that.
                    await _unitOfWork.BeginTransactionAsync();
                    await _unitOfWork.CommitAsync();
                    throw new AuthenticateException("Refresh token was already used. Please log in again.");

                case RefreshResult.Expired:
                    throw new AuthenticateException("Refresh token has expired.");

                default:
                    throw new AuthenticateException("Invalid refresh token.");
            }

            // Generate new access token
            var token = _tokenService.GenerateToken(user);

            // Apply persistence
            await _unitOfWork.BeginTransactionAsync();
            await _unitOfWork.CommitAsync();

            return new TokenDTO
            {
                Token = token,
                RefreshToken = newRefreshToken
            };
        }
    }
}
