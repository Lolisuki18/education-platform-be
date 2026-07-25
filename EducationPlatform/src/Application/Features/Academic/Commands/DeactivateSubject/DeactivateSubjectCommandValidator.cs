using FluentValidation;

namespace Application.Features.Academic.Commands.DeactivateSubject
{
    public class DeactivateSubjectCommandValidator : AbstractValidator<DeactivateSubjectCommand>
    {
        public DeactivateSubjectCommandValidator()
        {
            RuleFor(x => x.SubjectID)
                .NotEmpty().WithMessage("SubjectID is required.");
        }
    }
}
