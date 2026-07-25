using FluentValidation;

namespace Application.Features.Courses.ReviewCourse
{
    public class ReviewCourseCommandValidator : AbstractValidator<ReviewCourseCommand>
    {
        public ReviewCourseCommandValidator()
        {
            RuleFor(x => x.CourseID)
                .NotEmpty().WithMessage("CourseID is required.");

            RuleFor(x => x.AdminNote)
                .MaximumLength(1000).WithMessage("AdminNote must not exceed 1000 characters.");

            RuleForEach(x => x.ViolatedPolicyIDs)
                .NotEmpty().WithMessage("ViolatedPolicyID cannot be empty.");

            RuleForEach(x => x.ViolatedChapters)
                .ChildRules(chapter =>
                {
                    chapter.RuleFor(c => c.ViolatedChapterId)
                        .NotEmpty().WithMessage("ViolatedChapterId is required.");
                    chapter.RuleFor(c => c.AdminNote)
                        .MaximumLength(1000).WithMessage("Chapter AdminNote must not exceed 1000 characters.");
                });
        }
    }
}
