using Domain.DomainExceptions;

namespace Domain.CourseManagement.ValueObject
{
    public sealed class CoursePrice
    {
        #region Attributes
        #endregion

        #region Properities
        public decimal Amount { get; }
        #endregion

        private CoursePrice(decimal amount)
        {
            Amount = amount;
        }

        #region Methods
        public static CoursePrice Free()
        {
            return new CoursePrice(0);
        }

        public static CoursePrice Paid(decimal amount)
        {
            if (amount <= 0)
                throw new DomainException(
                    "Course price must be greater than zero");

            return new CoursePrice(amount);
        }

        public bool IsFree()
        {
            return Amount == 0;
        }

        public override bool Equals(object? obj)
        {
            return obj is CoursePrice other && Amount == other.Amount;
        }

        public override int GetHashCode()
        {
            return Amount.GetHashCode();
        }
        #endregion
    }
}

