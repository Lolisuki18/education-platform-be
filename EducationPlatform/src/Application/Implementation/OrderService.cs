using AutoMapper;
using Application.BusinessException;
using Application.Results;
using Application.Commands.Order;
using Application.Queries.Order;
using Application.Interface;
using Infrastructure.Interface;
using Infrastructure.Persistence.Seeds;
using Domain.EnrollmentManagement.Aggregate;
using Domain.IdentityManagement.ValueObject;
using Domain.OrderManagement.Aggregate;
using Domain.OrderManagement.ValueObject;
using Domain.CourseManagement.Aggregate;

namespace Application.Implementation
{
    public class OrderService : IOrderService
    {
        #region Attributes
        private readonly IMapper mapper;
        private readonly IUnitOfWork unitOfWork;
        #endregion

        #region Properties
        #endregion

        public OrderService(
            IMapper mapper,
            IUnitOfWork unitOfWork)
        {
            this.mapper = mapper;
            this.unitOfWork = unitOfWork;
        }

        #region Methods
        public async Task<IEnumerable<OrderDTO>> GetOrders(
            QueryOrderDto dto,
            Guid callerId,
            string callerRole)
        {
            // Role parsing
            if (!Enum.TryParse<Role>(callerRole, true, out var role))
                throw new AuthenticateException("Invalid role");

            // Teacher scoping
            Guid? teacherId = role == Role.Teacher ? callerId : null;

            // Query
            var list = await unitOfWork
                .GetRepository<IOrderRepository>()
                .GetOrders(
                    dto.OrderStatus,
                    dto.PageIndex,
                    dto.PageSize,
                    teacherId);

            if (list == null || !list.Any())
                throw new NotFound("Order list is not found or empty");

            return mapper.Map<IEnumerable<OrderDTO>>(list);
        }

        public async Task<OrderDTO> CreateOrder(CreateOrderDto dto)
        {
            // Validate course existence
            var course = await unitOfWork
                .GetRepository<ICourseRepository>()
                .GetByIdAsync(dto.CourseID);

            if (course == null)
                throw new NotFound(
                    $"Course with ID: {dto.CourseID} not found.");

            // Validate student existence
            var student = await unitOfWork
                .GetRepository<IUserRepository>()
                .GetByIdAsync(dto.StudentID);

            if (student == null)
                throw new NotFound(
                    $"Student with ID: {dto.StudentID} not found.");

            // Caculate discount from coupons
            decimal totalDiscount = 0;
            List<Coupon> validCoupons = new();

            if (dto.CouponIds != null && dto.CouponIds.Any())
            {
                foreach (var couponId in dto.CouponIds)
                {
                    var coupon = await unitOfWork
                        .GetRepository<IOrderRepository>()
                        .GetCouponDetailById(couponId);

                    if (coupon == null)
                        continue;

                    // Validate coupon
                    if (coupon.StudentID != dto.StudentID || coupon.IsUsed)
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
                dto.StudentID,
                dto.CourseID,
                null);

            // Apply domain - Mark coupons as used
            foreach (var coupon in validCoupons)
            {
                coupon.MarkAsUsed();
            }

            // Apply persistence
            await unitOfWork.BeginTransactionAsync();
            unitOfWork
                .GetRepository<IOrderRepository>()
                .Add(order);
            await unitOfWork.CommitAsync();

            // Return OrderDTO
            return mapper.Map<OrderDTO>(order);
        }

        public async Task<OrderDTO> FinishOrder(long orderCode)
        {
            // Validate order existence
            var order = await unitOfWork
                .GetRepository<IOrderRepository>()
                .GetOrderByOrderCode(orderCode);

            if (order == null)
                throw new NotFound(
                    $"Order with code: {orderCode} not found.");

            // Validate course existence
            var course = await unitOfWork
                .GetRepository<ICourseRepository>()
                .GetCourseDetailByID(order.CourseID);

            if (course == null)
                throw new NotFound(
                    $"Course with ID: {order.CourseID} not found");

            // Apply domain: start student enrollment and update order status
            order.StudentPaid(null);

            var enrollment = new Enrollment(
                Guid.NewGuid(),
                order.StudentID,
                order.CourseID,
                null);

            // Apply persistence
            await unitOfWork.BeginTransactionAsync();
            unitOfWork
                .GetRepository<IOrderRepository>()
                .Update(order.OrderID, order);
            unitOfWork
                .GetRepository<IEnrollmentRepository>()
                .Add(enrollment);
            await unitOfWork.CommitAsync();

            // Return OrderDTO
            return mapper.Map<OrderDTO>(order);
        }

        public async Task<IEnumerable<CouponDTO>> GetCoupons(
            Guid callerId,
            string callerRole)
        {
            // Role parsing
            if (!Enum.TryParse<Role>(callerRole, true, out var role))
                throw new AuthenticateException("Invalid role");

            // Student scoping
            Guid? studentId = role == Role.Student ? callerId : null;

            // Query
            var list = await unitOfWork
                .GetRepository<IOrderRepository>()
                .GetCoupons(
                    studentId);

            return mapper.Map<IEnumerable<CouponDTO>>(list);
        }
        #endregion
    }
}


