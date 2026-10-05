using Domain.Exceptions;
using Domain.OrderManagement.Enum;

namespace Domain.OrderManagement.Aggregate
{
    public class Coupon
    {
        #region Properties
        public Guid CouponID { get; private set; }
        public Guid? StudentID { get; private set; }
        public string Code { get; private set; } = string.Empty;
        public decimal DiscountAmount { get; private set; }
        public bool IsUsed { get; private set; }
        public string Reason { get; private set; } = string.Empty;
        public string Description { get; private set; } = string.Empty;
        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }

        public DateTime StartDate { get; private set; }
        public DateTime ExpiredDate { get; private set; }
        public int MaxUsage { get; private set; }
        public int CurrentUsage { get; private set; }
        public bool IsActive { get; private set; }
        public CouponType Type { get; private set; }
        public int Version { get; private set; }
        #endregion

        protected Coupon() { }

        // Constructor 1: Student Compensation Coupon
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
            Code = code.Trim().ToUpper();
            DiscountAmount = discountAmount;
            Reason = reason.Trim();
            Description = reason.Trim();
            IsUsed = false;
            CreatedAt = DateTime.UtcNow;

            // Default fields for compensation coupon
            Type = CouponType.Compensation;
            StartDate = DateTime.UtcNow;
            ExpiredDate = DateTime.UtcNow.AddYears(1);
            MaxUsage = 1;
            CurrentUsage = 0;
            IsActive = true;
            Version = 1;
        }

        // Constructor 2: Admin/Marketing Coupon
        public Coupon(
            Guid couponId,
            string code,
            string description,
            decimal discountAmount,
            DateTime startDate,
            DateTime expiredDate,
            int maxUsage)
        {
            if (couponId == Guid.Empty)
                throw new DomainException("Coupon ID cannot be empty");

            if (string.IsNullOrWhiteSpace(code))
                throw new DomainException("Coupon code is required");

            if (code.Trim().Length < 3 || code.Trim().Length > 50)
                throw new DomainException("Coupon code length must be between 3 and 50 characters");

            if (discountAmount <= 0)
                throw new DomainException("Discount amount must be greater than zero");

            if (maxUsage <= 0)
                throw new DomainException("Maximum usage count must be greater than zero");

            if (startDate >= expiredDate)
                throw new DomainException("Start date must be earlier than expiration date");

            if (expiredDate <= DateTime.UtcNow)
                throw new DomainException("Coupon expiration date must be greater than current UTC time");

            CouponID = couponId;
            StudentID = null;
            Code = code.Trim().ToUpper();
            DiscountAmount = discountAmount;
            Description = description?.Trim() ?? string.Empty;
            Reason = string.Empty;
            IsUsed = false;
            CreatedAt = DateTime.UtcNow;

            Type = CouponType.Marketing;
            StartDate = startDate;
            ExpiredDate = expiredDate;
            MaxUsage = maxUsage;
            CurrentUsage = 0;
            IsActive = true;
            Version = 1;
        }

        #region Methods
        public void UpdateDetails(
            string description,
            decimal discountAmount,
            DateTime startDate,
            DateTime expiredDate,
            int maxUsage)
        {
            if (discountAmount <= 0)
                throw new DomainException("Discount amount must be greater than zero");

            if (maxUsage <= 0)
                throw new DomainException("Maximum usage count must be greater than zero");

            if (startDate >= expiredDate)
                throw new DomainException("Start date must be earlier than expiration date");

            // Allow setting expiration date in the past only if it's already in the past (updating other fields)
            // But if it's a new expired date, it must be in the future
            if (expiredDate <= DateTime.UtcNow && expiredDate != ExpiredDate)
                throw new DomainException("Coupon expiration date must be greater than current UTC time");

            Description = description?.Trim() ?? string.Empty;
            DiscountAmount = discountAmount;
            StartDate = startDate;
            ExpiredDate = expiredDate;
            MaxUsage = maxUsage;
            UpdatedAt = DateTime.UtcNow;
            Version++;
        }

        public void Activate()
        {
            if (ExpiredDate <= DateTime.UtcNow)
                throw new DomainException("Expired coupons cannot be reactivated.");

            IsActive = true;
            UpdatedAt = DateTime.UtcNow;
            Version++;
        }

        public void Deactivate()
        {
            IsActive = false;
            UpdatedAt = DateTime.UtcNow;
            Version++;
        }

        public void MarkAsUsed()
        {
            if (CurrentUsage >= MaxUsage)
                throw new DomainException("Coupon already used");

            CurrentUsage++;
            IsUsed = CurrentUsage >= MaxUsage;
            UpdatedAt = DateTime.UtcNow;
            Version++;
        }

        /// <summary>Gives back one use, e.g. when the order that consumed it was never paid.</summary>
        public void Release()
        {
            if (CurrentUsage <= 0)
                return;

            CurrentUsage--;
            IsUsed = CurrentUsage >= MaxUsage;
            UpdatedAt = DateTime.UtcNow;
            Version++;
        }

        public bool CanBeApplied()
        {
            return IsActive
                && DateTime.UtcNow >= StartDate
                && DateTime.UtcNow <= ExpiredDate
                && CurrentUsage < MaxUsage;
        }
        #endregion
    }
}
