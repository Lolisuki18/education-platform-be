using MediatR;
using Application.Results;
using Infrastructure.Interface;
using Application.BusinessException;
using Application.Helper;

namespace Application.Features.Identity.Commands.RefreshToken
{
    public class RefreshTokenCommand : IRequest<TokenDTO>
    {
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, TokenDTO>
    {
        private readonly IUnitOfWork _unitOfWork;

        public RefreshTokenCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
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
            var token = TokenGenerator.GenerateToken(user);

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
