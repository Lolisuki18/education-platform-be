using Application.Exceptions;
using Application.Interface;
using Domain.Common.Interfaces;
using Domain.OrderManagement.Aggregate;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Coupons.Commands.CreateCoupon
{
    public class CreateCouponCommand : IRequest<Guid>
    {
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal DiscountAmount { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime ExpiredDate { get; set; }
        public int MaxUsage { get; set; }
    }

    public class CreateCouponCommandHandler : IRequestHandler<CreateCouponCommand, Guid>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;

        public CreateCouponCommandHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
        }

        public async Task<Guid> Handle(CreateCouponCommand request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            var repo = _unitOfWork.GetRepository<ICouponRepository>();

            if (await repo.ExistsByCodeAsync(request.Code))
                throw new ConflictException($"Coupon code '{request.Code}' already exists.");

            var coupon = new Coupon(
                Guid.NewGuid(),
                request.Code,
                request.Description,
                request.DiscountAmount,
                request.StartDate,
                request.ExpiredDate,
                request.MaxUsage
            );

            repo.Add(coupon);

            try
            {
                await _unitOfWork.CommitAsync(_currentUser.Id.Value.ToString());
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConflictException("The coupon was modified by another user. Please refresh and try again.");
            }

            return coupon.CouponID;
        }
    }
}
