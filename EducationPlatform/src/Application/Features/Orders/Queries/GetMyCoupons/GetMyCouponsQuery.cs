using Application.Results;
using MediatR;
using Domain.Common.Interfaces;
using AutoMapper;
using Domain.IdentityManagement.Enum;
using Application.BusinessException;
using Application.Interface;
using Domain.OrderManagement.Aggregate;

namespace Application.Features.Orders.Queries.GetCoupons
{
    public class GetCouponsQuery : IRequest<IEnumerable<CouponDTO>>
    {
    }

    public class GetCouponsQueryHandler : IRequestHandler<GetCouponsQuery, IEnumerable<CouponDTO>>
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

        public async Task<IEnumerable<CouponDTO>> Handle(GetCouponsQuery request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue || string.IsNullOrEmpty(_currentUser.Role))
                throw new AuthenticateException("User must be authenticated.");

            // Role parsing
            if (!Enum.TryParse<Role>(_currentUser.Role, true, out var role))
                throw new AuthenticateException("Invalid role");

            // Student scoping
            Guid? studentId = role == Role.Student ? _currentUser.Id : null;

            var list = await _unitOfWork
                .GetRepository<IOrderRepository>()
                .GetCoupons(studentId);

            return _mapper.Map<IEnumerable<CouponDTO>>(list);
        }
    }
}
