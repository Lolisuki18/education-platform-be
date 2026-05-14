using Application.Results;
using Application.Commands.Order;
using Application.Queries.Order;

namespace Application.Interface
{
    public interface IOrderService
    {
        Task<IEnumerable<OrderDTO>> GetOrders(
            QueryOrderDto dto,
            Guid callerId,
            string callerRole);

        Task<OrderDTO> CreateOrder(
            CreateOrderDto dto);

        Task<OrderDTO> FinishOrder(
            long orderCode);

        Task<IEnumerable<CouponDTO>> GetCoupons(
            Guid callerId,
            string callerRole);
    }
}


