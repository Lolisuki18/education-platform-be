namespace API.Models.Orders
{
    public class ReturnOrderResponseDto
    {
        public long OrderCode { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
