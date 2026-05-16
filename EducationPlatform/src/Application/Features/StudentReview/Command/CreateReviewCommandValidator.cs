using FluentValidation;

namespace Application.Features.StudentReview.Command
{
    public class CreateReviewCommandValidator : AbstractValidator<CreateReviewCommand>
    {
        public CreateReviewCommandValidator()
        {
            RuleFor(v => v.Rating)
                .InclusiveBetween(1, 5).WithMessage("Rating must be between 1 and 5.");

            RuleFor(v => v.Comment)
                .MaximumLength(1000).WithMessage("Comment must not exceed 1000 characters.");
        }
    }
}
