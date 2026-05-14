using Domain.DomainExceptions;

namespace Domain.OrderManagement.Aggregate
{
    public class Coupon
    {
        #region Properties
        public Guid CouponID { get; private set; }
        public Guid StudentID { get; private set; }
        public string Code { get; private set; } = string.Empty;
        public decimal DiscountAmount { get; private set; }
        public bool IsUsed { get; private set; }
        public string Reason { get; private set; } = string.Empty;
        public DateTime CreatedAt { get; private set; }
        #endregion

        protected Coupon() { }

        public Coupon(
            Guid couponId,
            Guid studentId,
            string code,
            decimal discountAmount,
            string reason)
        {
            if (couponId == Guid.Empty)
                throw new DomainException("Coupon ID cannot be empty");

            if (studentId == Guid.Empty)
                throw new DomainException("Student ID cannot be empty");

            if (string.IsNullOrWhiteSpace(code))
                throw new DomainException("Coupon code is required");

            if (discountAmount <= 0)
                throw new DomainException("Discount amount must be greater than zero");

            if (string.IsNullOrWhiteSpace(reason))
                throw new DomainException("Coupon reason is required");

            CouponID = couponId;
            StudentID = studentId;
            Code = code.Trim();
            DiscountAmount = discountAmount;
            Reason = reason.Trim();
            IsUsed = false;
            CreatedAt = DateTime.UtcNow;
        }

        #region Methods
        public void MarkAsUsed()
        {
            if (IsUsed)
                throw new DomainException("Coupon already used");

            IsUsed = true;
        }
        #endregion
    }
}
