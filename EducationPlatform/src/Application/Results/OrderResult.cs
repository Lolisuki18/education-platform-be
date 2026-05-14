using Domain.OrderManagement.Enum;

namespace Application.Results
{
    public class OrderDTO
    {
        public Guid OrderID { get; set; }
        public long OrderCode { get; set; }
        public decimal PlatformAmount { get; set; }
        public decimal TeacherAmount { get; set; }
        public OrderMethod Method { get; set; }
        public OrderStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public Guid StudentID { get; set; }
        public Guid CourseID { get; set; }
        public string? CheckoutUrl { get; set; }
    }

    public class CouponDTO
    {
        public Guid CouponID { get; set; }
        public Guid StudentID { get; set; }
        public string Code { get; set; } = string.Empty;
        public decimal DiscountAmount { get; set; }
        public bool IsUsed { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}


