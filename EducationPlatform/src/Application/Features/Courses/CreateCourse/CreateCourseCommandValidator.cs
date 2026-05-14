using FluentValidation;

namespace Application.Features.Courses.CreateCourse
{
    public class CreateCourseCommandValidator : AbstractValidator<CreateCourseCommand>
    {
        public CreateCourseCommandValidator()
        {
            RuleFor(v => v.Title)
                .NotEmpty().WithMessage("Title is required.")
                .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

            RuleFor(v => v.Price)
                .GreaterThanOrEqualTo(0).WithMessage("Price must be a positive value.");

            RuleFor(v => v.Description)
                .NotEmpty().WithMessage("Description is required.");

            RuleFor(v => v.ThumbnailName)
                .NotEmpty().WithMessage("Thumbnail is required.");
        }
    }
}
