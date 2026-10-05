using MediatR;
using Application.Results;
using Domain.Common.Interfaces;
using AutoMapper;
using Application.Exceptions;
using Domain.OrderManagement.Aggregate;
using Domain.OrderManagement.Enum;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Orders.Commands.FinishOrder
{
    public class FinishOrderCommand : IRequest<OrderDTO>
    {
        public long OrderCode { get; set; }

        /// <summary>Amount reported by the payment gateway. When set it must match the order total.</summary>
        public long? PaidAmount { get; set; }
    }

    public class FinishOrderCommandHandler : IRequestHandler<FinishOrderCommand, OrderDTO>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<FinishOrderCommandHandler> _logger;

        public FinishOrderCommandHandler(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ILogger<FinishOrderCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<OrderDTO> Handle(FinishOrderCommand request, CancellationToken cancellationToken)
        {
            // Validate order existence
            var order = await _unitOfWork
                .GetRepository<IOrderRepository>()
                .GetOrderByOrderCode(request.OrderCode);

            if (order == null)
                throw new NotFoundException($"Order with code: {request.OrderCode} not found.");

            // Gateways retry and the browser redirect races the webhook: finishing twice must be harmless
            if (order.IsPaid)
            {
                return _mapper.Map<OrderDTO>(order);
            }

            if (request.PaidAmount.HasValue && request.PaidAmount.Value != (long)Math.Round(order.TotalAmount))
            {
                throw new BadRequestException(
                    $"Paid amount {request.PaidAmount.Value} does not match the total of order {request.OrderCode}.");
            }

            if (order.Status == OrderStatus.Cancelled)
            {
                _logger.LogWarning(
                    "Order {OrderCode} was cancelled before its payment arrived; activating it because the student paid.",
                    order.OrderCode);
            }

            // Apply domain: update order status and raise OrderPaidEvent
            order.StudentPaid(null);

            // Apply persistence
            try
            {
                await _unitOfWork.BeginTransactionAsync();
                await _unitOfWork.GetRepository<IOrderRepository>().UpdateAsync(order.OrderID, order, cancellationToken);
                await _unitOfWork.CommitAsync(order.StudentID.ToString());
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another request finished (or cancelled) the same order between our read and write
                _logger.LogInformation("Order {OrderCode} was changed concurrently while finishing it.", order.OrderCode);
            }

            return _mapper.Map<OrderDTO>(order);
        }
    }
}
