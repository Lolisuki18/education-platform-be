using Application.BusinessException;
using Application.Interface;
using Domain.Common.Interfaces;
using Domain.OrderManagement.Aggregate;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Coupons.Commands.UpdateCoupon
{
    public class UpdateCouponCommand : IRequest<Unit>
    {
        public Guid CouponId { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal DiscountAmount { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime ExpiredDate { get; set; }
        public int MaxUsage { get; set; }
    }

    public class UpdateCouponCommandHandler : IRequestHandler<UpdateCouponCommand, Unit>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;

        public UpdateCouponCommandHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
        }

        public async Task<Unit> Handle(UpdateCouponCommand request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            var repo = _unitOfWork.GetRepository<ICouponRepository>();

            var coupon = await repo.GetByIdAsync(request.CouponId, cancellationToken);
            if (coupon == null)
                throw new NotFound("Coupon not found.");

            // Update details
            try
            {
                coupon.UpdateDetails(
                    request.Description,
                    request.DiscountAmount,
                    request.StartDate,
                    request.ExpiredDate,
                    request.MaxUsage
                );
            }
            catch (Domain.DomainExceptions.DomainException ex)
            {
                throw new Conflict(ex.Message);
            }

            await repo.UpdateAsync(request.CouponId, coupon, cancellationToken);

            try
            {
                await _unitOfWork.CommitAsync(_currentUser.Id.Value.ToString());
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new Conflict("The coupon was modified by another user. Please refresh and try again.");
            }

            return Unit.Value;
        }
    }
}
