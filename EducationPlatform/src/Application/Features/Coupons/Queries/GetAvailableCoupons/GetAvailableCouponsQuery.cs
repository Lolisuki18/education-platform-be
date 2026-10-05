using Application.Results;
using AutoMapper;
using Domain.Common.Interfaces;
using Domain.OrderManagement.Aggregate;
using Domain.OrderManagement.Enum;
using MediatR;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Coupons.Queries.GetAvailableCoupons
{
    public class GetAvailableCouponsQuery : IRequest<IEnumerable<CouponDTO>>
    {
    }

    public class GetAvailableCouponsQueryHandler : IRequestHandler<GetAvailableCouponsQuery, IEnumerable<CouponDTO>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public GetAvailableCouponsQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<CouponDTO>> Handle(GetAvailableCouponsQuery request, CancellationToken cancellationToken)
        {
            var repo = _unitOfWork.GetRepository<ICouponRepository>();

            // Fetch marketing active coupons
            var (coupons, _) = await repo.GetPagedAsync(1, 1000, null, true, CouponType.Marketing, cancellationToken);

            // Filter by domain rules
            var availableCoupons = coupons.Where(c => c.CanBeApplied()).ToList();

            return _mapper.Map<IEnumerable<CouponDTO>>(availableCoupons);
        }
    }
}
