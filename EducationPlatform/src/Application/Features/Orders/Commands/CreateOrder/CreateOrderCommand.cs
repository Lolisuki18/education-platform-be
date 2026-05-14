using MediatR;
using Application.Results;
using Domain.Common.Interfaces;
using AutoMapper;
using Application.BusinessException;
using Domain.CourseManagement.Aggregate;
using Domain.OrderManagement.Aggregate;
using Domain.OrderManagement.ValueObject;
using Application.Interface;

namespace Application.Features.Orders.Commands.CreateOrder
{
    public class CreateOrderCommand : IRequest<OrderDTO>
    {
        public Guid CourseID { get; set; }
        public List<Guid>? CouponIds { get; set; }
    }

    public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, OrderDTO>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ICurrentUser _currentUser;
        private readonly IPaymentService _paymentService;

        public CreateOrderCommandHandler(
            IUnitOfWork unitOfWork, 
            IMapper mapper, 
            ICurrentUser currentUser,
            IPaymentService paymentService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _currentUser = currentUser;
            _paymentService = paymentService;
        }

        public async Task<OrderDTO> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated to create an order.");

            Guid studentId = _currentUser.Id.Value;

            // Validate course existence
            var course = await _unitOfWork
                .GetRepository<ICourseRepository>()
                .GetByIdAsync(request.CourseID);

            if (course == null)
                throw new NotFound($"Course with ID: {request.CourseID} not found.");

            // Calculate discount from coupons
            decimal totalDiscount = 0;
            List<Coupon> validCoupons = new();

            if (request.CouponIds != null && request.CouponIds.Any())
            {
                foreach (var couponId in request.CouponIds)
                {
                    var coupon = await _unitOfWork
                        .GetRepository<IOrderRepository>()
                        .GetCouponDetailById(couponId);

                    if (coupon == null)
                        continue;

                    // Validate coupon
                    if (coupon.StudentID != studentId || coupon.IsUsed)
                        continue;

                    totalDiscount += coupon.DiscountAmount;
                    validCoupons.Add(coupon);
                }
            }

            // Apply discount
            decimal finalPrice = Math.Max(0, course.Price.Amount - totalDiscount);

            // Apply domain - create commission from course price
            var commission = Commission.Create(
                Order.PLATFORM_COMMISSION_RATE,
                finalPrice);

            // Apply domain - create Order
            var order = new Order(
                Guid.NewGuid(),
                commission,
                studentId,
                request.CourseID,
                null);

            // Apply domain - Mark coupons as used
            foreach (var coupon in validCoupons)
            {
                coupon.MarkAsUsed();
            }

            // Apply persistence
            await _unitOfWork.BeginTransactionAsync();
            _unitOfWork.GetRepository<IOrderRepository>().Add(order);
            await _unitOfWork.CommitAsync();

            // Generate Payment Link
            string checkoutUrl = await _paymentService.CreatePaymentLinkAsync(
                order.OrderCode,
                finalPrice,
                $"Order {order.OrderCode} for course {course.Title}"
            );

            var dto = _mapper.Map<OrderDTO>(order);
            dto.CheckoutUrl = checkoutUrl;

            return dto;
        }
    }
}
