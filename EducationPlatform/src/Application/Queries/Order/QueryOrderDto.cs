namespace Application.Queries.Order
{
    public class QueryOrderDto
    {
        public string? OrderStatus { get; set; } = string.Empty;
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 1;
    }
}
