using Domain.Common;

namespace Domain.OrderManagement.Events
{
    public class OrderPaidEvent : IDomainEvent
    {
        public Guid OrderID { get; }
        public Guid StudentID { get; }
        public Guid CourseID { get; }
        public DateTime PaidAt { get; }

        public OrderPaidEvent(Guid orderID, Guid studentID, Guid courseID, DateTime paidAt)
        {
            OrderID = orderID;
            StudentID = studentID;
            CourseID = courseID;
            PaidAt = paidAt;
        }
    }
}
