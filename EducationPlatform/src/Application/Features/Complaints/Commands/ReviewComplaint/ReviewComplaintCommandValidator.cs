using FluentValidation;

namespace Application.Features.Complaints.Commands.ReviewComplaint
{
    public class ReviewComplaintCommandValidator : AbstractValidator<ReviewComplaintCommand>
    {
        public ReviewComplaintCommandValidator()
        {
            RuleFor(x => x.ComplaintID)
                .NotEmpty().WithMessage("ComplaintID is required.");

            RuleFor(x => x.AdminNote)
                .MaximumLength(1000).WithMessage("AdminNote must not exceed 1000 characters.");
        }
    }
}
