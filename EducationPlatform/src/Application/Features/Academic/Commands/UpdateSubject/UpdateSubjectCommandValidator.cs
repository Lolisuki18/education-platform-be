using FluentValidation;

namespace Application.Features.Academic.Commands.UpdateSubject
{
    public class UpdateSubjectCommandValidator : AbstractValidator<UpdateSubjectCommand>
    {
        public UpdateSubjectCommandValidator()
        {
            RuleFor(x => x.SubjectID)
                .NotEmpty().WithMessage("Subject ID is required.");

            RuleFor(x => x.Code)
                .NotEmpty().WithMessage("Subject code is required.")
                .MaximumLength(50).WithMessage("Subject code must not exceed 50 characters.");

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Subject name is required.")
                .MaximumLength(150).WithMessage("Subject name must not exceed 150 characters.");
        }
    }
}
