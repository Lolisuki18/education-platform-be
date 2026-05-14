using System.Collections.Generic;

namespace API.Models.Orders
{
    public class CreateOrderRequestDto
    {
        public Guid CourseId { get; set; }
        public List<Guid> SelectedCouponIds { get; set; } = new();
    }
}
