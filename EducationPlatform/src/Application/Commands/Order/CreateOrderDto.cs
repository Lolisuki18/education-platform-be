namespace Application.Commands.Order
{
    public class CreateOrderDto
    {
        public Guid StudentID { get; set; }
        public Guid CourseID { get; set; }
        public List<Guid>? CouponIds { get; set; } = new List<Guid>();
    }
}
