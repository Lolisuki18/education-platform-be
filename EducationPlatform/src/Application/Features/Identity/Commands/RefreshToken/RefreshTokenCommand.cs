using MediatR;
using Application.Results;
using Domain.Common.Interfaces;
using Application.BusinessException;
using Domain.IdentityManagement.Aggregate;
using Application.Helper;

using Microsoft.Extensions.Configuration;

namespace Application.Features.Identity.Commands.RefreshToken
{
    public class RefreshTokenCommand : IRequest<TokenDTO>
    {
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, TokenDTO>
    {
        private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

        private readonly Application.Interface.ITokenService _tokenService;
        private readonly IUnitOfWork _unitOfWork;

        public RefreshTokenCommandHandler(IUnitOfWork unitOfWork, Application.Interface.ITokenService tokenService)
        {
            _unitOfWork = unitOfWork;
            _tokenService = tokenService;
        }

        public async Task<TokenDTO> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
        {
            // Find user by refresh token
            var user = await _unitOfWork
                .GetRepository<IUserRepository>()
                .GetByRefreshToken(request.RefreshToken);

            if (user == null)
                throw new AuthenticateException("Invalid refresh token.");

            // Validate refresh token
            if (!user.CanRefresh(request.RefreshToken))
            {
                user.RevokeRefreshToken();
                await _unitOfWork.BeginTransactionAsync();
                await _unitOfWork.GetRepository<IUserRepository>().UpdateAsync(user.UserID, user, cancellationToken);
                await _unitOfWork.CommitAsync();

                throw new AuthenticateException("Refresh token has expired.");
            }

            // Generate new token
            var token = _tokenService.GenerateToken(user);

            // Generate new refresh token
            var newRefreshToken = _tokenService.GenerateRefreshToken();

            // Apply domain
            user.IssueRefreshToken(newRefreshToken, RefreshTokenLifetime);

            // Apply persistence
            await _unitOfWork.BeginTransactionAsync();
            await _unitOfWork.GetRepository<IUserRepository>().UpdateAsync(user.UserID, user, cancellationToken);
            await _unitOfWork.CommitAsync();

            return new TokenDTO
            {
                Token = token,
                RefreshToken = newRefreshToken
            };
        }
    }
}
