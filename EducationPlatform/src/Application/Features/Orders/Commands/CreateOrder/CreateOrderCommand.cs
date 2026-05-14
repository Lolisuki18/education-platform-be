using MediatR;
using Application.Results;
using Infrastructure.Interface;
using AutoMapper;
using Application.BusinessException;
using Domain.CourseManagement.Aggregate;
using Domain.OrderManagement.Aggregate;
using Domain.OrderManagement.ValueObject;
using Infrastructure.Persistence.Seeds;

namespace Application.Features.Orders.Commands.CreateOrder
{
    public class CreateOrderCommand : IRequest<OrderDTO>
    {
        public Guid CourseID { get; set; }
        public Guid StudentID { get; set; }
        public List<Guid>? CouponIds { get; set; }
    }

    public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, OrderDTO>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public CreateOrderCommandHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<OrderDTO> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
        {
            // Validate course existence
            var course = await _unitOfWork
                .GetRepository<ICourseRepository>()
                .GetByIdAsync(request.CourseID);

            if (course == null)
                throw new NotFound($"Course with ID: {request.CourseID} not found.");

            // Validate student existence
            var student = await _unitOfWork
                .GetRepository<IUserRepository>()
                .GetByIdAsync(request.StudentID);

            if (student == null)
                throw new NotFound($"Student with ID: {request.StudentID} not found.");

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
                    if (coupon.StudentID != request.StudentID || coupon.IsUsed)
                        continue;

                    totalDiscount += coupon.DiscountAmount;
                    validCoupons.Add(coupon);
                }
            }

            // Apply discount
            decimal finalPrice = Math.Max(0, course.Price.Amount - totalDiscount);

            // Apply domain - create commission from course price
            var commission = Commission.Create(
                EnrollmentSeeder.PLATFORM_COMMISSION_RATE,
                finalPrice);

            // Apply domain - create Order
            var order = new Order(
                Guid.NewGuid(),
                commission,
                request.StudentID,
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

            return _mapper.Map<OrderDTO>(order);
        }
    }
}
