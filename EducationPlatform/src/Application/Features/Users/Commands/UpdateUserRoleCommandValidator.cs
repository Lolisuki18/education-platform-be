using FluentValidation;

namespace Application.Features.Users.Commands
{
    public class UpdateUserRoleCommandValidator : AbstractValidator<UpdateUserRoleCommand>
    {
        public UpdateUserRoleCommandValidator()
        {
            RuleFor(x => x.Role)
                .IsInEnum().WithMessage("Invalid role value.");
        }
    }
}
