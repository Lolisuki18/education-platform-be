using FluentValidation;

namespace Application.Features.Complaints.Commands.CreateComplaint
{
    public class CreateComplaintCommandValidator : AbstractValidator<CreateComplaintCommand>
    {
        public CreateComplaintCommandValidator()
        {
            RuleFor(x => x.CourseID)
                .NotEmpty().WithMessage("CourseID is required.");

            RuleFor(x => x.Reason)
                .NotEmpty().WithMessage("Reason is required.")
                .MaximumLength(2000).WithMessage("Reason must not exceed 2000 characters.");
        }
    }
}
