using MediatR;
using Domain.Common.Interfaces;
using Application.BusinessException;
using Application.Helper;
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
                throw new Conflict($"User with phone {request.Phone} already exists.");
            }

            // Cannot self-register admin
            if (request.Role == (int)Role.Admin)
                throw new Conflict("Cannot register as Admin.");

            await _unitOfWork.BeginTransactionAsync();

            User user;

            if (userByEmail != null)
            {
                if (userByEmail.IsVerified)
                    throw new Conflict($"User with email {request.Email} already exists.");

                // Reuse unverified user
                user = userByEmail;
                user.GenerateEmailOtp(TimeSpan.FromMinutes(5));
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
                    DateTime.Now
                );

                user.GenerateEmailOtp(TimeSpan.FromMinutes(5));
                userRepo.Add(user);
            }

            await _unitOfWork.CommitAsync();

            // Send email (outside transaction)
            await _emailService.SendVerificationEmailAsync(user.Email, user.EmailOtp!);

            return Unit.Value;
        }
    }
}
