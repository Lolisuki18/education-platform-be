using Domain.Common.Interfaces;

namespace Domain.OrderManagement.Aggregate
{
    public interface IOrderRepository :
        IGenericRepository<Order>
    {
        Task<IEnumerable<Order>> GetOrders(
            string? status,
            int pageIndex,
            int pageSize,
            Guid? teacherId,
            Guid? studentId,
            CancellationToken cancellationToken = default);

        Task<Order?> GetOrderByOrderCode(
            long orderCode,
            CancellationToken cancellationToken = default);

        Task<IEnumerable<Penalty>> GetPenalties(
            Guid? teacherId,
            CancellationToken cancellationToken = default);

        void CreatePenalty(
            Penalty penalty);

        Task<IEnumerable<Coupon>> GetCoupons(
            Guid? studentId,
            CancellationToken cancellationToken = default);

        Task<Coupon?> GetCouponDetailById(
            Guid couponId,
            CancellationToken cancellationToken = default);

        /// <summary>Loads (tracked) every coupon in one query.</summary>
        Task<List<Coupon>> GetCouponsByIds(
            IEnumerable<Guid> couponIds,
            CancellationToken cancellationToken = default);

        /// <summary>The order of this student for this course that is still waiting for payment, if any.</summary>
        Task<Order?> GetAwaitingPaymentOrder(
            Guid studentId,
            Guid courseId,
            CancellationToken cancellationToken = default);

        /// <summary>Whether the student has an order that is still waiting for payment inside its payment window.</summary>
        Task<bool> HasOpenOrderAsync(
            Guid studentId,
            DateTime now,
            CancellationToken cancellationToken = default);

        /// <summary>Unpaid orders created before <paramref name="createdBefore"/>.</summary>
        Task<List<Order>> GetUnpaidOrdersCreatedBefore(
            DateTime createdBefore,
            int take,
            CancellationToken cancellationToken = default);

        void CreateCoupons(
            IEnumerable<Coupon> coupons);

        Task<(int Total, int Commission, int TeacherFinance)> Summary(
            DateTime? from,
            DateTime? to,
            CancellationToken cancellationToken = default);

        Task<Dictionary<string, List<(string Label, decimal Value)>>> AnalyticsGrowth(
            DateTime? from,
            DateTime? to,
            string groupBy,
            string revenueType,
            CancellationToken cancellationToken = default);

        Task<List<(Guid CourseId, string CourseName, decimal Revenue)>>
            GetTopCoursesByRevenue(
                DateTime? from,
                DateTime? to,
                Guid? gradeId,
                Guid? subjectId,
                int top,
                CancellationToken cancellationToken = default);

        Task<List<(Guid SubjectId, string SubjectName, decimal Revenue)>>
            GetTopSubjectsByRevenue(
                DateTime? from,
                DateTime? to,
                Guid? gradeId,
                int top,
                CancellationToken cancellationToken = default);

        Task<List<(Guid GradeId, string GradeName, decimal Revenue)>>
            GetTopGradesByRevenue(
                DateTime? from,
                DateTime? to,
                Guid? subjectId,
                int top,
                CancellationToken cancellationToken = default);
    }
}
