namespace API.Models.Orders
{
    public class ListOrdersRequestDto
    {
        public string? Status { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 6;
    }
}
