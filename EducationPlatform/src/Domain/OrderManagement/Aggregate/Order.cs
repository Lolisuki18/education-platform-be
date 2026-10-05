using Domain.Common;
using Domain.CourseManagement.Aggregate;
using Domain.Exceptions;
using Domain.IdentityManagement.Aggregate;
using Domain.OrderManagement.Enum;
using Domain.OrderManagement.Events;
using Domain.OrderManagement.ValueObject;

namespace Domain.OrderManagement.Aggregate
{
    public class Order : BaseEntity
    {
        #region Attributes
        public const decimal PLATFORM_COMMISSION_RATE = 0.15m;

        /// <summary>How long the PayOS payment link stays valid after the order is created.</summary>
        public static readonly TimeSpan PaymentWindow = TimeSpan.FromMinutes(15);
        #endregion

        #region Properties
        public Guid OrderID { get; private set; }
        public long OrderCode { get; private set; }
        public decimal PlatformAmount { get; private set; }
        public decimal TeacherAmount { get; private set; }
        public OrderMethod Method { get; private set; }
        public OrderStatus Status { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? PaidAt { get; private set; }
        public string? CheckoutUrl { get; private set; }

        /// <summary>Coupons consumed by this order, so they can be given back if it is never paid.</summary>
        public List<Guid> CouponIds { get; private set; } = new();

        public Guid StudentID { get; private set; }
        public Guid CourseID { get; private set; }

        public User User { get; private set; }
        public Course Course { get; private set; }
        #endregion

        protected Order() { }

        public Order(
            Guid orderId,
            Commission commission,
            Guid studentId,
            Guid courseId,
            DateTime? createdAt,
            IEnumerable<Guid>? couponIds = null)
        {
            if (orderId == Guid.Empty)
                throw new DomainException(
                    "Order ID cannot be empty");

            if (studentId == Guid.Empty)
                throw new DomainException(
                    "Student ID cannot be empty");

            if (courseId == Guid.Empty)
                throw new DomainException(
                    "Course ID cannot be empty");

            OrderID = orderId;
            OrderCode = GenerateOrderCode();
            PlatformAmount = commission.PlatformAmount;
            TeacherAmount = commission.TeacherAmount;
            Method = OrderMethod.PayOS;
            Status = OrderStatus.Created;
            CreatedAt = createdAt ?? DateTime.UtcNow;
            StudentID = studentId;
            CourseID = courseId;
            CouponIds = couponIds?.Distinct().ToList() ?? new List<Guid>();
        }

        #region Methods
        /// <summary>
        /// Milliseconds since epoch plus three random digits, so two orders created in the same millisecond
        /// do not collide. Stays below 2^53 (PayOS / JavaScript safe integer limit) until the year 2255.
        /// </summary>
        private static long GenerateOrderCode()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000
                   + System.Security.Cryptography.RandomNumberGenerator.GetInt32(1000);
        }

        public bool IsPaid => Status == OrderStatus.Pending;

        public decimal TotalAmount => PlatformAmount + TeacherAmount;

        public DateTime PaymentExpiresAt => CreatedAt.Add(PaymentWindow);

        /// <summary>True while the order can still be paid through the link it was given.</summary>
        public bool IsAwaitingPayment(DateTime now) =>
            Status == OrderStatus.Created && now < PaymentExpiresAt;

        public void AttachCheckoutUrl(string checkoutUrl)
        {
            if (string.IsNullOrWhiteSpace(checkoutUrl))
                throw new DomainException("Checkout URL cannot be empty");

            CheckoutUrl = checkoutUrl;
        }

        public void StudentPaid(DateTime? paidAt)
        {
            if (Status == OrderStatus.Pending)
                return;

            // A cancelled order can still be paid late: PayOS is the source of truth for the money.
            Status = OrderStatus.Pending;
            PaidAt = paidAt ?? DateTime.UtcNow;

            AddDomainEvent(new OrderPaidEvent(OrderID, StudentID, CourseID, PaidAt.Value));
        }

        public void Cancel()
        {
            if (Status != OrderStatus.Created)
                throw new DomainException("Only orders that are awaiting payment can be cancelled");

            Status = OrderStatus.Cancelled;
        }
        #endregion
    }
}

