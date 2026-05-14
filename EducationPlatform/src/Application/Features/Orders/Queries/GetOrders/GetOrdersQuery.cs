using Application.Results;
using MediatR;
using Infrastructure.Interface;
using AutoMapper;
using Domain.OrderManagement.Enum;
using Domain.IdentityManagement.ValueObject;
using Application.BusinessException;

namespace Application.Features.Orders.Queries.GetOrders
{
    public class GetOrdersQuery : IRequest<IEnumerable<OrderDTO>>
    {
        public OrderStatus? OrderStatus { get; set; }
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public Guid CallerId { get; set; }
        public string CallerRole { get; set; } = string.Empty;
    }

    public class GetOrdersQueryHandler : IRequestHandler<GetOrdersQuery, IEnumerable<OrderDTO>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public GetOrdersQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<OrderDTO>> Handle(GetOrdersQuery request, CancellationToken cancellationToken)
        {
            // Role parsing
            if (!Enum.TryParse<Role>(request.CallerRole, true, out var role))
                throw new AuthenticateException("Invalid role");

            // Teacher scoping
            Guid? teacherId = role == Role.Teacher ? request.CallerId : null;

            var list = await _unitOfWork
                .GetRepository<IOrderRepository>()
                .GetOrders(
                    request.OrderStatus?.ToString(),
                    request.PageIndex,
                    request.PageSize,
                    teacherId);

            if (list == null || !list.Any())
                throw new NotFound("Order list is not found or empty");

            return _mapper.Map<IEnumerable<OrderDTO>>(list);
        }
    }
}
