using FluentValidation;
using System;

namespace Application.Features.Coupons.Commands.CreateCoupon
{
    public class CreateCouponCommandValidator : AbstractValidator<CreateCouponCommand>
    {
        public CreateCouponCommandValidator()
        {
            RuleFor(x => x.Code)
                .NotEmpty().WithMessage("Coupon code is required.")
                .Length(3, 50).WithMessage("Coupon code length must be between 3 and 50 characters.");

            RuleFor(x => x.DiscountAmount)
                .GreaterThan(0).WithMessage("Discount amount must be greater than zero.")
                .LessThanOrEqualTo(1_000_000_000m).WithMessage("Discount amount is too large.")
                .Must(amount => amount == decimal.Truncate(amount)).WithMessage("Discount amount must be a whole number.");

            RuleFor(x => x.MaxUsage)
                .GreaterThan(0).WithMessage("Maximum usage count must be greater than zero.");

            RuleFor(x => x.StartDate)
                .LessThan(x => x.ExpiredDate).WithMessage("Start date must be earlier than expiration date.");

            RuleFor(x => x.ExpiredDate)
                .GreaterThan(DateTime.UtcNow).WithMessage("Coupon expiration date must be greater than current UTC time.");
        }
    }
}
