using Application.Results;
using MediatR;
using Domain.Common.Interfaces;
using Domain.CourseManagement.Aggregate;
using Domain.IdentityManagement.Aggregate;
using Domain.EnrollmentManagement.Aggregate;
using Domain.OrderManagement.Aggregate;
using Application.BusinessException;
using System.Linq;

namespace Application.Features.Statistics.Queries.GetSummaryStatistic
{
    public class GetSummaryStatisticQuery : IRequest<SummaryStatisticDTO>
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
    }

    public class GetSummaryStatisticQueryHandler : IRequestHandler<GetSummaryStatisticQuery, SummaryStatisticDTO>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetSummaryStatisticQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<SummaryStatisticDTO> Handle(GetSummaryStatisticQuery request, CancellationToken cancellationToken)
        {
            var userRepo = _unitOfWork.GetRepository<IUserRepository>();
            var courseRepo = _unitOfWork.GetRepository<ICourseRepository>();
            var enrollmentRepo = _unitOfWork.GetRepository<IEnrollmentRepository>();
            var orderRepo = _unitOfWork.GetRepository<IOrderRepository>();

            // User
            var (totalUsers, totalTeachers, totalStudents) = await userRepo.Summary(request.From, request.To);

            // Course
            var (inReview, rejected, published, courseGradeDict, courseSubjectDict) = await courseRepo.Summary(request.From, request.To);

            // Enrollment
            var (totalEnrollments, completedEnrollments, enrollmentGradeDict, enrollmentSubjectDict) = await enrollmentRepo.Summary(request.From, request.To);

            // Revenue
            var (totalRevenue, commission, teacherFinance) = await orderRepo.Summary(request.From, request.To);

            return new SummaryStatisticDTO
            {
                User = new SummaryUserDTO
                {
                    Total = totalUsers,
                    TeacherCount = totalTeachers,
                    StudentCount = totalStudents
                },
                Course = new SummaryCourseDTO
                {
                    Total = inReview + rejected + published,
                    InReviewCount = inReview,
                    RejectedCount = rejected,
                    PublishedCount = published
                },
                Enrollment = new SummaryEnrollmentDTO
                {
                    Total = totalEnrollments,
                    Completed = completedEnrollments,
                    NotCompleted = totalEnrollments - completedEnrollments
                },
                Revenue = new SummaryRevenueDTO
                {
                    Total = totalRevenue,
                    Commission = commission,
                    TeacherFinance = teacherFinance
                },
                CourseByGrade = new SummaryCourseGradeDTO
                {
                    Total = courseGradeDict.Values.Sum(),
                    GradeCounts = courseGradeDict
                },
                CourseBySubject = new SummaryCourseSubjectDTO
                {
                    Total = courseSubjectDict.Values.Sum(),
                    SubjectCounts = courseSubjectDict
                },
                EnrollmentByGrade = new SummaryEnrollmentGradeDTO
                {
                    Total = enrollmentGradeDict.Values.Sum(),
                    GradeCounts = enrollmentGradeDict
                },
                EnrollmentBySubject = new SummaryEnrollmentSubjectDTO
                {
                    Total = enrollmentSubjectDict.Values.Sum(),
                    SubjectCounts = enrollmentSubjectDict
                }
            };
        }
    }
}
