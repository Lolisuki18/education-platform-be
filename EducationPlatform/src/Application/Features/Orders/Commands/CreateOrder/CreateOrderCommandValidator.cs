using FluentValidation;

namespace Application.Features.Orders.Commands.CreateOrder
{
    public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
    {
        public CreateOrderCommandValidator()
        {
            RuleFor(x => x.CourseID)
                .NotEmpty().WithMessage("CourseID is required.");

            RuleForEach(x => x.CouponIds)
                .NotEmpty().WithMessage("CouponId cannot be empty.");
        }
    }
}
