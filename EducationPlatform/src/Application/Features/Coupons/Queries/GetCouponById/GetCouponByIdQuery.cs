using Application.BusinessException;
using Application.Interface;
using Application.Results;
using AutoMapper;
using Domain.Common.Interfaces;
using Domain.OrderManagement.Aggregate;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Coupons.Queries.GetCouponById
{
    public class GetCouponByIdQuery : IRequest<CouponDTO>
    {
        public Guid CouponId { get; set; }
    }

    public class GetCouponByIdQueryHandler : IRequestHandler<GetCouponByIdQuery, CouponDTO>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ICurrentUser _currentUser;

        public GetCouponByIdQueryHandler(IUnitOfWork unitOfWork, IMapper mapper, ICurrentUser currentUser)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _currentUser = currentUser;
        }

        public async Task<CouponDTO> Handle(GetCouponByIdQuery request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            var coupon = await _unitOfWork
                .GetRepository<ICouponRepository>()
                .GetByIdAsync(request.CouponId);

            if (coupon == null)
                throw new NotFound("Coupon not found.");

            return _mapper.Map<CouponDTO>(coupon);
        }
    }
}
