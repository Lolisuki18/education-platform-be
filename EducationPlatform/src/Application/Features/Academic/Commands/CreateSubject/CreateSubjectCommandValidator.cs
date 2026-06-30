using FluentValidation;

namespace Application.Features.Academic.Commands.CreateSubject
{
    public class CreateSubjectCommandValidator : AbstractValidator<CreateSubjectCommand>
    {
        public CreateSubjectCommandValidator()
        {
            RuleFor(x => x.Code)
                .NotEmpty().WithMessage("Subject code is required.")
                .MaximumLength(50).WithMessage("Subject code must not exceed 50 characters.");

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Subject name is required.")
                .MaximumLength(150).WithMessage("Subject name must not exceed 150 characters.");
        }
    }
}
