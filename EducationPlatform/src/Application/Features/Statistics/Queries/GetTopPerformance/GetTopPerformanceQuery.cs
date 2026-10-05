using Application.Results;
using MediatR;
using Domain.Common.Interfaces;
using Domain.EnrollmentManagement.Aggregate;
using Domain.OrderManagement.Aggregate;
using System.Linq;

namespace Application.Features.Statistics.Queries.GetTopPerformance
{
    public class GetTopPerformanceQuery : IRequest<TopPerformanceDTO>, Application.Common.Behaviors.ICachedQuery
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public Guid? GradeId { get; set; }
        public Guid? SubjectId { get; set; }
        public int Top { get; set; } = 10;
    }

    public class GetTopPerformanceQueryHandler : IRequestHandler<GetTopPerformanceQuery, TopPerformanceDTO>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetTopPerformanceQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<TopPerformanceDTO> Handle(GetTopPerformanceQuery request, CancellationToken cancellationToken)
        {
            var orderRepo = _unitOfWork.GetRepository<IOrderRepository>();
            var enrollmentRepo = _unitOfWork.GetRepository<IEnrollmentRepository>();

            var topCoursesByEnrollment = await enrollmentRepo.GetTopCoursesByEnrollment(
                request.From, request.To, request.GradeId, request.SubjectId, request.Top, cancellationToken);

            var topSubjectsByEnrollment = await enrollmentRepo.GetTopSubjectsByEnrollment(
                request.From, request.To, request.GradeId, request.Top, cancellationToken);

            var topGradesByEnrollment = await enrollmentRepo.GetTopGradesByEnrollment(
                request.From, request.To, request.SubjectId, request.Top, cancellationToken);

            var topCoursesByRevenue = await orderRepo.GetTopCoursesByRevenue(
                request.From, request.To, request.GradeId, request.SubjectId, request.Top, cancellationToken);

            var topSubjectsByRevenue = await orderRepo.GetTopSubjectsByRevenue(
                request.From, request.To, request.GradeId, request.Top, cancellationToken);

            var topGradesByRevenue = await orderRepo.GetTopGradesByRevenue(
                request.From, request.To, request.SubjectId, request.Top, cancellationToken);

            return new TopPerformanceDTO
            {
                CoursesByEnrollment = topCoursesByEnrollment.Select(x => new TopCourseByEnrollmentDTO
                {
                    CourseId = x.CourseId,
                    CourseName = x.CourseName,
                    EnrollmentCount = x.EnrollmentCount
                }).ToList(),

                CoursesByRevenue = topCoursesByRevenue.Select(x => new TopCourseByRevenueDTO
                {
                    CourseId = x.CourseId,
                    CourseName = x.CourseName,
                    Revenue = x.Revenue
                }).ToList(),

                SubjectsByEnrollment = topSubjectsByEnrollment.Select(x => new TopSubjectByEnrollmentDTO
                {
                    SubjectId = x.SubjectId,
                    SubjectName = x.SubjectName,
                    EnrollmentCount = x.EnrollmentCount
                }).ToList(),

                SubjectsByRevenue = topSubjectsByRevenue.Select(x => new TopSubjectByRevenueDTO
                {
                    SubjectId = x.SubjectId,
                    SubjectName = x.SubjectName,
                    Revenue = x.Revenue
                }).ToList(),

                GradesByEnrollment = topGradesByEnrollment.Select(x => new TopGradeByEnrollmentDTO
                {
                    GradeId = x.GradeId,
                    GradeName = x.GradeName,
                    EnrollmentCount = x.EnrollmentCount
                }).ToList(),

                GradesByRevenue = topGradesByRevenue.Select(x => new TopGradeByRevenueDTO
                {
                    GradeId = x.GradeId,
                    GradeName = x.GradeName,
                    Revenue = x.Revenue
                }).ToList()
            };
        }
    }
}
