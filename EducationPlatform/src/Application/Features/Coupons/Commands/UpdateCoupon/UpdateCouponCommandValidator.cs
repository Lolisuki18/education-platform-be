using FluentValidation;
using System;

namespace Application.Features.Coupons.Commands.UpdateCoupon
{
    public class UpdateCouponCommandValidator : AbstractValidator<UpdateCouponCommand>
    {
        public UpdateCouponCommandValidator()
        {
            RuleFor(x => x.CouponId)
                .NotEmpty().WithMessage("Coupon ID is required.");

            RuleFor(x => x.DiscountAmount)
                .GreaterThan(0).WithMessage("Discount amount must be greater than zero.")
                .LessThanOrEqualTo(1_000_000_000m).WithMessage("Discount amount is too large.")
                .Must(amount => amount == decimal.Truncate(amount)).WithMessage("Discount amount must be a whole number.");

            RuleFor(x => x.MaxUsage)
                .GreaterThan(0).WithMessage("Maximum usage count must be greater than zero.");

            RuleFor(x => x.StartDate)
                .LessThan(x => x.ExpiredDate).WithMessage("Start date must be earlier than expiration date.");
        }
    }
}
