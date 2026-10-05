using Application.Results;
using MediatR;
using Domain.Common.Interfaces;
using Domain.CourseManagement.Aggregate;
using Domain.IdentityManagement.Aggregate;
using Domain.EnrollmentManagement.Aggregate;
using Domain.OrderManagement.Aggregate;
using Application.Enums;
using System.Linq;

namespace Application.Features.Statistics.Queries.GetAnalyticsNormalizedGrowth
{
    public class GetAnalyticsNormalizedGrowthQuery : IRequest<AnalyticsGrowthDTO>, Application.Common.Behaviors.ICachedQuery
    {
        public AnalyticsGrowthType Type { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public AnalyticGroupDate GroupBy { get; set; }
        public string? UserRole { get; set; }
        public Guid? CourseGradeId { get; set; }
        public Guid? CourseSubjectId { get; set; }
        public Guid? EnrollmentGradeId { get; set; }
        public Guid? EnrollmentSubjectId { get; set; }
        public AnalyticRevenueType? RevenueType { get; set; }
    }

    public class GetAnalyticsNormalizedGrowthQueryHandler : IRequestHandler<GetAnalyticsNormalizedGrowthQuery, AnalyticsGrowthDTO>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetAnalyticsNormalizedGrowthQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<AnalyticsGrowthDTO> Handle(GetAnalyticsNormalizedGrowthQuery request, CancellationToken cancellationToken)
        {
            var userRepo = _unitOfWork.GetRepository<IUserRepository>();
            var courseRepo = _unitOfWork.GetRepository<ICourseRepository>();
            var enrollmentRepo = _unitOfWork.GetRepository<IEnrollmentRepository>();
            var orderRepo = _unitOfWork.GetRepository<IOrderRepository>();

            var userRaw = await userRepo.AnalyticsGrowth(request.From, request.To, request.GroupBy.ToString(), request.UserRole);
            var courseRaw = await courseRepo.AnalyticsGrowth(request.From, request.To, request.GroupBy.ToString(), request.CourseGradeId, request.CourseSubjectId);
            var enrollmentRaw = await enrollmentRepo.AnalyticsGrowth(request.From, request.To, request.GroupBy.ToString(), request.EnrollmentGradeId, request.EnrollmentSubjectId);
            var revenueRaw = await orderRepo.AnalyticsGrowth(request.From, request.To, request.GroupBy.ToString(), (request.RevenueType ?? AnalyticRevenueType.All).ToString());

            Dictionary<string, decimal> Flatten(Dictionary<string, List<(string Label, decimal Value)>> raw)
            {
                var flattened = raw.FirstOrDefault();
                if (flattened.Value == null) return new Dictionary<string, decimal>();

                return flattened.Value
                    .GroupBy(x => x.Label)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Value));
            }

            var userDict = Flatten(userRaw);
            var courseDict = Flatten(courseRaw);
            var enrollmentDict = Flatten(enrollmentRaw);
            var revenueDict = Flatten(revenueRaw);

            var allLabels = userDict.Keys
                .Union(courseDict.Keys)
                .Union(enrollmentDict.Keys)
                .Union(revenueDict.Keys)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            List<AnalyticsGrowthPointDTO> Normalize(Dictionary<string, decimal> dict)
            {
                var firstValue = dict.OrderBy(x => x.Key).FirstOrDefault().Value;

                return allLabels.Select(label =>
                {
                    var value = dict.ContainsKey(label) ? dict[label] : 0;

                    return new AnalyticsGrowthPointDTO
                    {
                        Label = label,
                        Value = firstValue == 0
                            ? 0
                            : Math.Round((value / firstValue) * 100, 2)
                    };
                }).ToList();
            }

            var series = new List<AnalyticsGrowthSeriesDTO>
            {
                new AnalyticsGrowthSeriesDTO { SeriesName = "Users", Data = Normalize(userDict) },
                new AnalyticsGrowthSeriesDTO { SeriesName = "Courses", Data = Normalize(courseDict) },
                new AnalyticsGrowthSeriesDTO { SeriesName = "Enrollments", Data = Normalize(enrollmentDict) },
                new AnalyticsGrowthSeriesDTO { SeriesName = "Revenue", Data = Normalize(revenueDict) }
            };

            return new AnalyticsGrowthDTO
            {
                Type = request.Type,
                Series = series
            };
        }
    }
}
