using Application.Results;
using MediatR;
using Domain.Common.Interfaces;
using AutoMapper;
using Domain.OrderManagement.Enum;
using Domain.IdentityManagement.Enum;
using Application.Exceptions;
using Application.Interface;
using Domain.OrderManagement.Aggregate;

namespace Application.Features.Orders.Queries.GetOrders
{
    public class GetOrdersQuery : IRequest<IEnumerable<OrderDTO>>
    {
        public OrderStatus? OrderStatus { get; set; }
        private int _pageIndex = 1;
        public int PageIndex
        {
            get => _pageIndex;
            set => _pageIndex = Application.Common.Paging.NormalizePageIndex(value);
        }
        private int _pageSize = Application.Common.Paging.DefaultPageSize;
        public int PageSize
        {
            get => _pageSize;
            set => _pageSize = Application.Common.Paging.NormalizePageSize(value);
        }
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
                    studentId, cancellationToken);

            if (list == null || !list.Any())
                throw new NotFoundException("Order list is not found or empty");

            var orders = _mapper.Map<IEnumerable<OrderDTO>>(list).ToList();

            // Teachers and admins also see these orders; the payment link belongs to the student who created it
            foreach (var order in orders)
            {
                order.CheckoutUrl = null;
            }

            return orders;
        }
    }
}
