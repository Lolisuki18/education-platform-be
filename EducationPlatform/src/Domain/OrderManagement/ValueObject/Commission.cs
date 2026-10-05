using Domain.Exceptions;

namespace Domain.OrderManagement.ValueObject
{
    public class Commission
    {
        #region Attributes
        #endregion

        #region Properties
        public decimal PlatformRate { get; }
        public decimal PlatformAmount { get; }
        public decimal TeacherAmount { get; }
        #endregion

        private Commission(decimal rate, decimal total)
        {
            PlatformRate = rate;
            PlatformAmount = total * rate;
            TeacherAmount = total - PlatformAmount;
        }

        #region Methods
        public static Commission Create(decimal rate, decimal total)
        {
            if (rate < 0 || rate > 1)
                throw new DomainException(
                    "Invalid commission rate");

            // Zero is allowed: a coupon can cover the whole price.
            if (total < 0)
                throw new DomainException(
                    "Total amount cannot be negative");

            return new Commission(rate, total);
        }
        #endregion
    }
}

