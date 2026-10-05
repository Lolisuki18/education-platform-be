using MediatR;
using Application.Results;
using Domain.Common.Interfaces;
using AutoMapper;
using Application.Exceptions;
using Domain.CourseManagement.Aggregate;
using Domain.OrderManagement.Aggregate;
using Domain.OrderManagement.ValueObject;
using Application.Interface;
using Domain.EnrollmentManagement.Aggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
        private readonly ILogger<CreateOrderCommandHandler> _logger;

        public CreateOrderCommandHandler(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ICurrentUser currentUser,
            IPaymentService paymentService,
            ILogger<CreateOrderCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _currentUser = currentUser;
            _paymentService = paymentService;
            _logger = logger;
        }

        public async Task<OrderDTO> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated to create an order.");

            Guid studentId = _currentUser.Id.Value;
            var orderRepository = _unitOfWork.GetRepository<IOrderRepository>();

            // Validate course existence
            var course = await _unitOfWork
                .GetRepository<ICourseRepository>()
                .GetByIdAsync(request.CourseID);

            if (course == null)
                throw new NotFoundException($"Course with ID: {request.CourseID} not found.");

            // Check if student is already enrolled in this course
            var alreadyEnrolled = await _unitOfWork
                .GetRepository<IEnrollmentRepository>()
                .IsStudentEnrolled(studentId, request.CourseID, cancellationToken);
            if (alreadyEnrolled)
            {
                throw new ConflictException("Student is already enrolled in this course.");
            }

            // A student has at most one order awaiting payment per course: hand back its link while it is valid
            var awaiting = await orderRepository.GetAwaitingPaymentOrder(studentId, request.CourseID);
            if (awaiting != null)
            {
                if (awaiting.IsAwaitingPayment(DateTime.UtcNow) && !string.IsNullOrEmpty(awaiting.CheckoutUrl))
                    return _mapper.Map<OrderDTO>(awaiting);

                // The previous attempt expired (or never got a link): free its coupons before starting over
                await CancelAsync(awaiting, orderRepository);
            }

            // Calculate discount from coupons (one query, invalid ones are skipped)
            var requestedCouponIds = request.CouponIds ?? new List<Guid>();
            var validCoupons = (await orderRepository.GetCouponsByIds(requestedCouponIds))
                .Where(c => c.CanBeApplied() && (!c.StudentID.HasValue || c.StudentID.Value == studentId))
                .ToList();

            decimal totalDiscount = validCoupons.Sum(c => c.DiscountAmount);

            // Apply discount
            decimal finalPrice = Math.Max(0, course.Price.Amount - totalDiscount);

            // Apply domain - create commission from the price the student actually pays
            var commission = Commission.Create(
                Order.PLATFORM_COMMISSION_RATE,
                finalPrice);

            // Apply domain - create Order
            var order = new Order(
                Guid.NewGuid(),
                commission,
                studentId,
                request.CourseID,
                null,
                validCoupons.Select(c => c.CouponID));

            // Apply domain - Mark coupons as used
            foreach (var coupon in validCoupons)
            {
                coupon.MarkAsUsed();
            }

            // Nothing to pay (free course or fully discounted): enroll straight away, PayOS is not involved
            if (finalPrice == 0)
            {
                order.StudentPaid(null);
            }

            // Apply persistence
            try
            {
                await _unitOfWork.BeginTransactionAsync();
                orderRepository.Add(order);
                await _unitOfWork.CommitAsync(order.IsPaid ? studentId.ToString() : null);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConflictException("A coupon was just used by another request. Please try again.");
            }
            catch (DbUpdateException)
            {
                throw new ConflictException("You already have an order awaiting payment for this course.");
            }

            if (order.IsPaid)
                return _mapper.Map<OrderDTO>(order);

            // Generate Payment Link. The order and its coupons are already saved, so a gateway failure must undo them.
            string checkoutUrl;
            try
            {
                checkoutUrl = await _paymentService.CreatePaymentLinkAsync(
                    order.OrderCode,
                    finalPrice,
                    $"Order {order.OrderCode} for course {course.Title}"
                );
            }
            catch
            {
                await TryCompensateAsync(order, orderRepository);
                throw;
            }

            order.AttachCheckoutUrl(checkoutUrl);
            await _unitOfWork.CommitAsync();

            return _mapper.Map<OrderDTO>(order);
        }

        private async Task CancelAsync(Order order, IOrderRepository orderRepository)
        {
            order.Cancel();

            foreach (var coupon in await orderRepository.GetCouponsByIds(order.CouponIds))
            {
                coupon.Release();
            }

            await _unitOfWork.BeginTransactionAsync();
            await _unitOfWork.CommitAsync();
        }

        private async Task TryCompensateAsync(Order order, IOrderRepository orderRepository)
        {
            try
            {
                await CancelAsync(order, orderRepository);
            }
            catch (Exception ex)
            {
                // The expired-order sweeper cancels it later, so the original payment error is the one to surface.
                _logger.LogError(ex, "Failed to cancel order {OrderCode} after the payment link could not be created.", order.OrderCode);
            }
        }
    }
}
