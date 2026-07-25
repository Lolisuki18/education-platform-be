using FluentValidation;

namespace Application.Features.Enrollments.Commands
{
    public class SubmitQuizCommandValidator : AbstractValidator<SubmitQuizCommand>
    {
        public SubmitQuizCommandValidator()
        {
            RuleFor(x => x.EnrollmentID)
                .NotEmpty().WithMessage("EnrollmentID is required.");

            RuleFor(x => x.ChapterID)
                .NotEmpty().WithMessage("ChapterID is required.");

            RuleFor(x => x.LessonID)
                .NotEmpty().WithMessage("LessonID is required.");

            RuleFor(x => x.QuizID)
                .NotEmpty().WithMessage("QuizID is required.");

            RuleFor(x => x.SelectedAnswers)
                .NotEmpty().WithMessage("At least one answer must be selected.");
        }
    }
}
