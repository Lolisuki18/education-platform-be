using FluentValidation;

namespace Application.Features.Users.Commands
{
    public class UpdateUserStatusCommandValidator : AbstractValidator<UpdateUserStatusCommand>
    {
        public UpdateUserStatusCommandValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("UserId is required.");
        }
    }
}
