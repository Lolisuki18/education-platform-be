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
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;

        public RefreshTokenCommandHandler(IUnitOfWork unitOfWork, IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _configuration = configuration;
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
                throw new AuthenticateException("Refresh token has expired.");

            // Generate new token
            var token = TokenGenerator.GenerateToken(user, _configuration);

            // Generate new refresh token
            var newRefreshToken = TokenGenerator.GenerateRefreshToken();

            // Apply domain
            user.IssueRefreshToken(newRefreshToken, TimeSpan.FromDays(7));

            // Apply persistence
            await _unitOfWork.BeginTransactionAsync();
            _unitOfWork.GetRepository<IUserRepository>().Update(user.UserID, user);
            await _unitOfWork.CommitAsync();

            return new TokenDTO
            {
                Token = token,
                RefreshToken = newRefreshToken
            };
        }
    }
}
