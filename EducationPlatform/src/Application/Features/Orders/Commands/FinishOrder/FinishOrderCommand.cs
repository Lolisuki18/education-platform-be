using MediatR;
using Application.Results;
using Domain.Common.Interfaces;
using AutoMapper;
using Application.BusinessException;
using Domain.OrderManagement.Aggregate;

namespace Application.Features.Orders.Commands.FinishOrder
{
    public class FinishOrderCommand : IRequest<OrderDTO>
    {
        public long OrderCode { get; set; }
    }

    public class FinishOrderCommandHandler : IRequestHandler<FinishOrderCommand, OrderDTO>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public FinishOrderCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<OrderDTO> Handle(FinishOrderCommand request, CancellationToken cancellationToken)
        {
            // Validate order existence
            var order = await _unitOfWork
                .GetRepository<IOrderRepository>()
                .GetOrderByOrderCode(request.OrderCode);

            if (order == null)
                throw new NotFound($"Order with code: {request.OrderCode} not found.");

            // Apply domain: update order status and raise OrderPaidEvent
            order.StudentPaid(null);

            // Apply persistence
            await _unitOfWork.BeginTransactionAsync();
            _unitOfWork.GetRepository<IOrderRepository>().Update(order.OrderID, order);

            // The DomainEventDispatcherInterceptor will catch the OrderPaidEvent 
            // and dispatch it to the OrderPaidEventHandler during CommitAsync.
            await _unitOfWork.CommitAsync(order.StudentID.ToString());

            return _mapper.Map<OrderDTO>(order);
        }
    }
}
