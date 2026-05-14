using Application.Results;
using MediatR;
using Infrastructure.Interface;
using AutoMapper;
using Domain.IdentityManagement.ValueObject;
using Application.BusinessException;

namespace Application.Features.Orders.Queries.GetCoupons
{
    public class GetCouponsQuery : IRequest<IEnumerable<CouponDTO>>
    {
        public Guid CallerId { get; set; }
        public string CallerRole { get; set; } = string.Empty;
    }

    public class GetCouponsQueryHandler : IRequestHandler<GetCouponsQuery, IEnumerable<CouponDTO>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public GetCouponsQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<CouponDTO>> Handle(GetCouponsQuery request, CancellationToken cancellationToken)
        {
            // Role parsing
            if (!Enum.TryParse<Role>(request.CallerRole, true, out var role))
                throw new AuthenticateException("Invalid role");

            // Student scoping
            Guid? studentId = role == Role.Student ? request.CallerId : null;

            var list = await _unitOfWork
                .GetRepository<IOrderRepository>()
                .GetCoupons(studentId);

            return _mapper.Map<IEnumerable<CouponDTO>>(list);
        }
    }
}
