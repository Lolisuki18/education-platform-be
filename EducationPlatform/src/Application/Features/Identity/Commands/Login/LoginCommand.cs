using MediatR;
using Application.Results;
using Domain.Common.Interfaces;
using Application.BusinessException;
using Domain.IdentityManagement.Aggregate;
using Application.Helper;

namespace Application.Features.Identity.Commands.Login
{
    public class LoginCommand : IRequest<TokenDTO>
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginCommandHandler : IRequestHandler<LoginCommand, TokenDTO>
    {
        private readonly IUnitOfWork _unitOfWork;

        public LoginCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<TokenDTO> Handle(LoginCommand request, CancellationToken cancellationToken)
        {
            // Validate user existence
            var user = await _unitOfWork
                .GetRepository<IUserRepository>()
                .GetUserByEmail(request.Email);
            
            if (user == null)
                throw new NotFound($"User with email: {request.Email} not found.");

            // Validate password and email verification
            if (!user.VerifyLogin(request.Password))
                throw new AuthenticateException("Invalid password or email has not been verified.");

            // Generate token
            var token = TokenGenerator.GenerateToken(user);

            // Generate refresh token
            var refreshToken = TokenGenerator.GenerateRefreshToken();

            // Apply domain
            user.IssueRefreshToken(refreshToken, TimeSpan.FromDays(7));

            // Apply persistence
            await _unitOfWork.BeginTransactionAsync();
            _unitOfWork.GetRepository<IUserRepository>().Update(user.UserID, user);
            await _unitOfWork.CommitAsync();

            return new TokenDTO
            {
                Token = token,
                RefreshToken = refreshToken
            };
        }
    }
}
