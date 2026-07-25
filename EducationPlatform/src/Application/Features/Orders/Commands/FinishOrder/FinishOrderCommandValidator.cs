using FluentValidation;

namespace Application.Features.Orders.Commands.FinishOrder
{
    public class FinishOrderCommandValidator : AbstractValidator<FinishOrderCommand>
    {
        public FinishOrderCommandValidator()
        {
            RuleFor(x => x.OrderCode)
                .GreaterThan(0).WithMessage("OrderCode must be a positive value.");
        }
    }
}
