using Domain.Common;
using Domain.CourseManagement.Aggregate;
using Domain.DomainExceptions;
using Domain.IdentityManagement.Aggregate;
using Domain.OrderManagement.Enum;
using Domain.OrderManagement.Events;
using Domain.OrderManagement.ValueObject;

namespace Domain.OrderManagement.Aggregate
{
    public class Order : BaseEntity
    {
        #region Attributes
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
            DateTime? createdAt)
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
            OrderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            PlatformAmount = commission.PlatformAmount;
            TeacherAmount = commission.TeacherAmount;
            Method = OrderMethod.PayOS;
            Status = OrderStatus.Created;
            CreatedAt = createdAt ?? DateTime.Now;
            StudentID = studentId;
            CourseID = courseId;
        }

        #region Methods
        public void StudentPaid(DateTime? paidAt)
        {
            Status = OrderStatus.Pending;
            PaidAt = paidAt ?? DateTime.Now;

            AddDomainEvent(new OrderPaidEvent(OrderID, StudentID, CourseID, PaidAt.Value));
        }
        #endregion
    }
}

