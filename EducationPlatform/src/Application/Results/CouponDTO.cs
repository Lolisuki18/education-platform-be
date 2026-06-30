using Domain.OrderManagement.Enum;
using System;

namespace Application.Results
{
    public class CouponDTO
    {
        public Guid Id { get; set; }
        // Keep CouponID for backward compatibility with existing tests and code
        public Guid CouponID { get; set; }
        public Guid? StudentID { get; set; }
        public string Code { get; set; } = string.Empty;
        public decimal DiscountAmount { get; set; }
        public bool IsUsed { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime ExpiredDate { get; set; }
        public int MaxUsage { get; set; }
        public int CurrentUsage { get; set; }
        public bool IsActive { get; set; }
        public CouponType Type { get; set; }
    }
}
