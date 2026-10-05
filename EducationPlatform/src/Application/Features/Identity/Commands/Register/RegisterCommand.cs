using MediatR;
using Domain.Common.Interfaces;
using Application.Exceptions;
using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.Enum;

namespace Application.Features.Identity.Commands.Register
{
    public class RegisterCommand : IRequest<Unit>
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public int Role { get; set; }
    }

    public class RegisterCommandHandler : IRequestHandler<RegisterCommand, Unit>
    {
        private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);

        /// <summary>Minimum time between two verification e-mails for the same address.</summary>
        public static readonly TimeSpan OtpResendCooldown = TimeSpan.FromSeconds(60);

        private readonly IUnitOfWork _unitOfWork;
        private readonly Application.Interface.IEmailService _emailService;

        public RegisterCommandHandler(IUnitOfWork unitOfWork, Application.Interface.IEmailService emailService)
        {
            _unitOfWork = unitOfWork;
            _emailService = emailService;
        }

        public async Task<Unit> Handle(RegisterCommand request, CancellationToken cancellationToken)
        {
            var userRepo = _unitOfWork.GetRepository<IUserRepository>();

            var userByEmail = await userRepo.GetUserByEmail(request.Email);
            var userByPhone = await userRepo.GetUserByPhone(request.Phone);

            // Phone must be globally unique
            if (userByPhone != null &&
                (userByEmail == null || userByPhone.UserID != userByEmail.UserID))
            {
                throw new ConflictException($"User with phone {request.Phone} already exists.");
            }

            // Cannot self-register admin
            if (request.Role == (int)Role.Admin)
                throw new ConflictException("Cannot register as Admin.");

            if (userByEmail != null)
            {
                if (userByEmail.IsVerified)
                    throw new ConflictException($"User with email {request.Email} already exists.");

                if (!userByEmail.CanRequestNewOtp(OtpLifetime, OtpResendCooldown))
                    throw new TooManyRequestsException("A verification code was sent a moment ago. Please wait before requesting another one.");
            }

            await _unitOfWork.BeginTransactionAsync();

            User user;
            string otp;

            if (userByEmail != null)
            {
                // Registering again with an unverified address replaces the earlier attempt completely
                user = userByEmail;
                user.ReissueRegistration(request.Password, request.Phone, request.Name, request.Bio, (Role)request.Role);
                otp = user.GenerateEmailOtp(OtpLifetime);
            }
            else
            {
                user = new User(
                    Guid.NewGuid(),
                    request.Email,
                    request.Password,
                    request.Phone,
                    request.Name,
                    request.Bio,
                    (Role)request.Role,
                    DateTime.UtcNow
                );

                otp = user.GenerateEmailOtp(OtpLifetime);
                userRepo.Add(user);
            }

            await _unitOfWork.CommitAsync();

            // Only the e-mail contains the code: the database keeps just a hash of it
            await _emailService.SendVerificationEmailAsync(user.Email, otp);

            return Unit.Value;
        }
    }
}
