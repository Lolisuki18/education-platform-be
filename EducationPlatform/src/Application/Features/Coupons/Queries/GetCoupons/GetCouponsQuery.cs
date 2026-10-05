using Application.Exceptions;
using Application.Interface;
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

namespace Application.Features.Coupons.Queries.GetCoupons
{
    public class GetCouponsQuery : IRequest<PagedResult<CouponDTO>>
    {
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? Search { get; set; }
        public bool? IsActive { get; set; }
        public CouponType? Type { get; set; }
    }

    public class GetCouponsQueryHandler : IRequestHandler<GetCouponsQuery, PagedResult<CouponDTO>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ICurrentUser _currentUser;

        public GetCouponsQueryHandler(IUnitOfWork unitOfWork, IMapper mapper, ICurrentUser currentUser)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _currentUser = currentUser;
        }

        public async Task<PagedResult<CouponDTO>> Handle(GetCouponsQuery request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            var (coupons, totalCount) = await _unitOfWork
                .GetRepository<ICouponRepository>()
                .GetPagedAsync(request.PageIndex, request.PageSize, request.Search, request.IsActive, request.Type, cancellationToken);

            var couponDtos = _mapper.Map<IEnumerable<CouponDTO>>(coupons).ToList();

            return new PagedResult<CouponDTO>
            {
                Items = couponDtos.AsReadOnly(),
                PageIndex = request.PageIndex,
                PageSize = request.PageSize,
                TotalItems = totalCount
            };
        }
    }
}
