using FluentValidation;

namespace Application.Features.Academic.Commands.UpdateGrade
{
    public class UpdateGradeCommandValidator : AbstractValidator<UpdateGradeCommand>
    {
        public UpdateGradeCommandValidator()
        {
            RuleFor(x => x.GradeID)
                .NotEmpty().WithMessage("Grade ID is required.");

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Grade name is required.")
                .MaximumLength(100).WithMessage("Grade name must not exceed 100 characters.");
        }
    }
}
