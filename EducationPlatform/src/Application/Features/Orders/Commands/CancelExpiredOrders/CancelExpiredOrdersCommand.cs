using MediatR;
using Domain.Common.Interfaces;
using Domain.OrderManagement.Aggregate;

namespace Application.Features.Orders.Commands.CancelExpiredOrders
{
    /// <summary>Cancels unpaid orders whose payment link has expired and gives their coupons back.</summary>
    public class CancelExpiredOrdersCommand : IRequest<int>
    {
        public int BatchSize { get; set; } = 50;
    }

    public class CancelExpiredOrdersCommandHandler : IRequestHandler<CancelExpiredOrdersCommand, int>
    {
        /// <summary>Extra time after the link expired, so a payment made at the last second can still arrive.</summary>
        public static readonly TimeSpan Grace = TimeSpan.FromMinutes(5);

        private readonly IUnitOfWork _unitOfWork;

        public CancelExpiredOrdersCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<int> Handle(CancelExpiredOrdersCommand request, CancellationToken cancellationToken)
        {
            var orderRepository = _unitOfWork.GetRepository<IOrderRepository>();

            var cutoff = DateTime.UtcNow - Order.PaymentWindow - Grace;
            var expired = await orderRepository.GetUnpaidOrdersCreatedBefore(cutoff, request.BatchSize);
            if (expired.Count == 0)
                return 0;

            foreach (var order in expired)
            {
                order.Cancel();

                foreach (var coupon in await orderRepository.GetCouponsByIds(order.CouponIds))
                {
                    coupon.Release();
                }
            }

            await _unitOfWork.BeginTransactionAsync();
            await _unitOfWork.CommitAsync();

            return expired.Count;
        }
    }
}
