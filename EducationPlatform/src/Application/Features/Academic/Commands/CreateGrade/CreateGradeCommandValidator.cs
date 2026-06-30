using FluentValidation;

namespace Application.Features.Academic.Commands.CreateGrade
{
    public class CreateGradeCommandValidator : AbstractValidator<CreateGradeCommand>
    {
        public CreateGradeCommandValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Grade name is required.")
                .MaximumLength(100).WithMessage("Grade name must not exceed 100 characters.");
        }
    }
}
