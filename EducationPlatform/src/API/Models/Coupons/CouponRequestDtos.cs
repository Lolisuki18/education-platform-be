using System;

namespace API.Models.Coupons
{
    public class CreateCouponRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal DiscountAmount { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime ExpiredDate { get; set; }
        public int MaxUsage { get; set; }
    }

    public class UpdateCouponRequest
    {
        public string Description { get; set; } = string.Empty;
        public decimal DiscountAmount { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime ExpiredDate { get; set; }
        public int MaxUsage { get; set; }
    }

    public class UpdateCouponStatusRequest
    {
        public bool IsActive { get; set; }
    }
}
