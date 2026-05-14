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
            Guid? teacherId);

        Task<Order?> GetOrderByOrderCode(
            long orderCode);

        Task<IEnumerable<Penalty>> GetPenalties(
            Guid? teacherId);

        void CreatePenalty(
            Penalty penalty);

        Task<IEnumerable<Coupon>> GetCoupons(
            Guid? studentId);

        Task<Coupon?> GetCouponDetailById(
            Guid couponId);

        void CreateCoupons(
            IEnumerable<Coupon> coupons);

        Task<(int Total, int Commission, int TeacherFinance)> Summary(
            DateTime? from,
            DateTime? to);

        Task<Dictionary<string, List<(string Label, decimal Value)>>> AnalyticsGrowth(
            DateTime? from,
            DateTime? to,
            string groupBy,
            string revenueType);

        Task<List<(Guid CourseId, string CourseName, decimal Revenue)>>
            GetTopCoursesByRevenue(
                DateTime? from,
                DateTime? to,
                Guid? gradeId,
                Guid? subjectId,
                int top);

        Task<List<(Guid SubjectId, string SubjectName, decimal Revenue)>>
            GetTopSubjectsByRevenue(
                DateTime? from,
                DateTime? to,
                Guid? gradeId,
                int top);

        Task<List<(Guid GradeId, string GradeName, decimal Revenue)>>
            GetTopGradesByRevenue(
                DateTime? from,
                DateTime? to,
                Guid? subjectId,
                int top);
    }
}
