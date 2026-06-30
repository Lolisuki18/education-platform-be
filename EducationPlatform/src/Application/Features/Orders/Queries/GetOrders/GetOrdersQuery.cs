using Application.Results;
using MediatR;
using Domain.Common.Interfaces;
using AutoMapper;
using Domain.OrderManagement.Enum;
using Domain.IdentityManagement.Enum;
using Application.BusinessException;
using Application.Interface;
using Domain.OrderManagement.Aggregate;

namespace Application.Features.Orders.Queries.GetOrders
{
    public class GetOrdersQuery : IRequest<IEnumerable<OrderDTO>>
    {
        public OrderStatus? OrderStatus { get; set; }
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
    }

    public class GetOrdersQueryHandler : IRequestHandler<GetOrdersQuery, IEnumerable<OrderDTO>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ICurrentUser _currentUser;

        public GetOrdersQueryHandler(IUnitOfWork unitOfWork, IMapper mapper, ICurrentUser currentUser)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _currentUser = currentUser;
        }

        public async Task<IEnumerable<OrderDTO>> Handle(GetOrdersQuery request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue || string.IsNullOrEmpty(_currentUser.Role))
                throw new AuthenticateException("User must be authenticated.");

            // Role parsing
            if (!Enum.TryParse<Role>(_currentUser.Role, true, out var role))
                throw new AuthenticateException("Invalid role");

            // Teacher scoping
            Guid? teacherId = role == Role.Teacher ? _currentUser.Id : null;

            // Student scoping
            Guid? studentId = role == Role.Student ? _currentUser.Id : null;

            var list = await _unitOfWork
                .GetRepository<IOrderRepository>()
                .GetOrders(
                    request.OrderStatus?.ToString(),
                    request.PageIndex,
                    request.PageSize,
                    teacherId,
                    studentId);

            if (list == null || !list.Any())
                throw new NotFound("Order list is not found or empty");

            return _mapper.Map<IEnumerable<OrderDTO>>(list);
        }
    }
}
