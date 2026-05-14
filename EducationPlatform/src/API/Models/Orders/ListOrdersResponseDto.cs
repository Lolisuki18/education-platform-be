using Application.Results;

namespace API.Models.Orders
{
    public class ListOrdersResponseDto
    {
        public IEnumerable<OrderDTO> Orders { get; set; } = new List<OrderDTO>();
    }
}
