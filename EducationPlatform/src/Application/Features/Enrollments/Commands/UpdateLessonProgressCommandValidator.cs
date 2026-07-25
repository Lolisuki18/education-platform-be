using FluentValidation;

namespace Application.Features.Enrollments.Commands
{
    public class UpdateLessonProgressCommandValidator : AbstractValidator<UpdateLessonProgressCommand>
    {
        public UpdateLessonProgressCommandValidator()
        {
            RuleFor(x => x.EnrollmentID)
                .NotEmpty().WithMessage("EnrollmentID is required.");

            RuleFor(x => x.ChapterID)
                .NotEmpty().WithMessage("ChapterID is required.");

            RuleFor(x => x.LessonID)
                .NotEmpty().WithMessage("LessonID is required.");
        }
    }
}
