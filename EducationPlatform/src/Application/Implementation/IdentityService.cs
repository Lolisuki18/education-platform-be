using AutoMapper;
using Application.BusinessException;
using Application.Results;
using Application.Commands.Identity;
using Application.Helper;
using Application.Interface;
using Infrastructure.Interface;
using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.ValueObject;

namespace Application.Implementation
{
    public class IdentityService : IIdentityService
    {
        #region Attributes
        private readonly IMapper mapper;
        private readonly IUnitOfWork unitOfWork;
        #endregion

        #region Properties
        #endregion

        public IdentityService(
            IMapper mapper,
            IUnitOfWork unitOfWork)
        {
            this.mapper = mapper;
            this.unitOfWork = unitOfWork;
        }

        #region Methods
        public async Task<TokenDTO> Login(LoginDto dto)
        {
            // Validate user existence
            var user = await unitOfWork
                .GetRepository<IUserRepository>()
                .GetUserByEmail(dto.Email);
            if (user == null)
                throw new NotFound(
                    $"User with email: {dto.Email} not found.");

            // Validate password and email verification
            if (!user.VerifyLogin(dto.Password))
                throw new AuthenticateException(
                    "Invalid password or email has not been verified.");

            // Generate token
            var token = TokenGenerator.GenerateToken(user);

            // Generate refresh token
            var refreshToken = TokenGenerator.GenerateRefreshToken();

            // Apply domain
            user.IssueRefreshToken(
                refreshToken,
                TimeSpan.FromDays(7));

            // Apply persistance
            await unitOfWork.BeginTransactionAsync();
            unitOfWork
                .GetRepository<IUserRepository>()
                .Update(user.UserID, user);
            await unitOfWork.CommitAsync();

            // Return token dto
            return new TokenDTO
            {
                Token = token,
                RefreshToken = refreshToken
            };
        }

        public async Task Register(RegisterDto dto)
        {
            var userRepo = unitOfWork.GetRepository<IUserRepository>();

            var userByEmail = await userRepo.GetUserByEmail(dto.Email);
            var userByPhone = await userRepo.GetUserByPhone(dto.Phone);

            // Phone must be globally unique
            if (userByPhone != null &&
                (userByEmail == null || userByPhone.UserID != userByEmail.UserID))
            {
                throw new Conflict($"User with phone {dto.Phone} already exists.");
            }

            // Cannot self-register admin
            if (dto.Role == (int)Role.Admin)
                throw new Conflict("Cannot register as Admin.");

            await unitOfWork.BeginTransactionAsync();

            User user;

            if (userByEmail != null)
            {
                if (userByEmail.IsVerified)
                    throw new Conflict($"User with email {dto.Email} already exists.");

                // Reuse unverified user
                user = userByEmail;
                user.GenerateEmailOtp(TimeSpan.FromMinutes(5));
            }
            else
            {
                user = new User(
                    Guid.NewGuid(),
                    dto.Email,
                    dto.Password,
                    dto.Phone,
                    dto.Name,
                    dto.Bio,
                    (Role)dto.Role,
                    DateTime.Now
                );

                user.GenerateEmailOtp(TimeSpan.FromMinutes(5));
                userRepo.Add(user);
            }

            await unitOfWork.CommitAsync();

            // Send email (outside transaction)
            await EmailHelper.SendVerificationEmailAsync(
                user.Email,
                user.EmailOtp!
            );
        }

        public async Task VerifyEmail(string otp)
        {
            // Validate user existence
            var user = await unitOfWork
                .GetRepository<IUserRepository>()
                .GetUserByOTP(otp);

            if (user == null)
                throw new NotFound("User not found.");

            // Apply domain
            user.VerifyEmail(otp);

            // Apply persistence
            await unitOfWork.BeginTransactionAsync();
            await unitOfWork.CommitAsync();
        }

        public async Task<TokenDTO> RefreshToken(string refreshToken)
        {
            // Find user by refresh token
            var user = await unitOfWork
                .GetRepository<IUserRepository>()
                .GetByRefreshToken(refreshToken);

            if (user == null)
                throw new AuthenticateException(
                    "Invalid refresh token.");

            // Validate refresh token
            if (!user.CanRefresh(refreshToken))
                throw new AuthenticateException
                    ("Refresh token has expired.");

            // Generate new token
            var token = TokenGenerator.GenerateToken(user);

            // Generate new refresh token
            var newRefreshToken = TokenGenerator.GenerateRefreshToken();

            // Apply domain
            user.IssueRefreshToken(
                newRefreshToken,
                TimeSpan.FromDays(7));

            // Apply persistance
            await unitOfWork.BeginTransactionAsync();
            unitOfWork
                .GetRepository<IUserRepository>()
                .Update(user.UserID, user);
            await unitOfWork.CommitAsync();

            // Return token dto
            return new TokenDTO
            {
                Token = token,
                RefreshToken = newRefreshToken
            };
        }
        #endregion
    }
}


