using FluentValidation;

namespace Application.Features.Academic.Commands.DeactivateGrade
{
    public class DeactivateGradeCommandValidator : AbstractValidator<DeactivateGradeCommand>
    {
        public DeactivateGradeCommandValidator()
        {
            RuleFor(x => x.GradeID)
                .NotEmpty().WithMessage("GradeID is required.");
        }
    }
}
