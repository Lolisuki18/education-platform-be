using Application.BusinessException;
using Application.Interface;
using Domain.Common.Interfaces;
using Domain.OrderManagement.Aggregate;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Coupons.Commands.UpdateCouponStatus
{
    public class UpdateCouponStatusCommand : IRequest<Unit>
    {
        public Guid CouponId { get; set; }
        public bool IsActive { get; set; }
    }

    public class UpdateCouponStatusCommandHandler : IRequestHandler<UpdateCouponStatusCommand, Unit>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;

        public UpdateCouponStatusCommandHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
        }

        public async Task<Unit> Handle(UpdateCouponStatusCommand request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            var repo = _unitOfWork.GetRepository<ICouponRepository>();

            var coupon = await repo.GetByIdAsync(request.CouponId, cancellationToken);
            if (coupon == null)
                throw new NotFound("Coupon not found.");

            if (request.IsActive)
            {
                try
                {
                    coupon.Activate();
                }
                catch (Domain.DomainExceptions.DomainException ex)
                {
                    throw new Conflict(ex.Message);
                }
            }
            else
            {
                coupon.Deactivate();
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
