using Application.Results;
using MediatR;
using Domain.Common.Interfaces;
using Domain.CourseManagement.Aggregate;
using Domain.IdentityManagement.Aggregate;
using Domain.EnrollmentManagement.Aggregate;
using Domain.OrderManagement.Aggregate;
using Application.Enums;
using System.Linq;

namespace Application.Features.Statistics.Queries.GetAnalyticsGrowth
{
    public class GetAnalyticsGrowthQuery : IRequest<AnalyticsGrowthDTO>, Application.Common.Behaviors.ICachedQuery
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
        public List<ComparisonRangeDTO>? ComparisonRanges { get; set; }
    }

    public class GetAnalyticsGrowthQueryHandler : IRequestHandler<GetAnalyticsGrowthQuery, AnalyticsGrowthDTO>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetAnalyticsGrowthQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<AnalyticsGrowthDTO> Handle(GetAnalyticsGrowthQuery request, CancellationToken cancellationToken)
        {
            var userRepo = _unitOfWork.GetRepository<IUserRepository>();
            var courseRepo = _unitOfWork.GetRepository<ICourseRepository>();
            var enrollmentRepo = _unitOfWork.GetRepository<IEnrollmentRepository>();
            var orderRepo = _unitOfWork.GetRepository<IOrderRepository>();

            async Task<(string Name, Dictionary<string, decimal>)> ExecuteRange(
                DateTime? from,
                DateTime? to,
                string name)
            {
                Dictionary<string, List<(string Label, decimal Value)>> raw;

                switch (request.Type)
                {
                    case AnalyticsGrowthType.User:
                        raw = await userRepo.AnalyticsGrowth(
                            from, to, request.GroupBy.ToString(), request.UserRole, cancellationToken);
                        break;

                    case AnalyticsGrowthType.Course:
                        raw = await courseRepo.AnalyticsGrowth(
                            from, to, request.GroupBy.ToString(),
                            request.CourseGradeId, request.CourseSubjectId, cancellationToken);
                        break;

                    case AnalyticsGrowthType.Enrollment:
                        raw = await enrollmentRepo.AnalyticsGrowth(
                            from, to, request.GroupBy.ToString(),
                            request.EnrollmentGradeId, request.EnrollmentSubjectId, cancellationToken);
                        break;

                    case AnalyticsGrowthType.Revenue:
                        var revenueType = request.RevenueType ?? AnalyticRevenueType.All;
                        raw = await orderRepo.AnalyticsGrowth(
                            from, to, request.GroupBy.ToString(),
                            revenueType.ToString(), cancellationToken);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }

                var flattened = raw.FirstOrDefault();

                var dict = flattened.Value?
                    .GroupBy(x => x.Label)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Sum(x => x.Value)
                    ) ?? new Dictionary<string, decimal>();

                return (name, dict);
            }

            var results = new List<(string Name, Dictionary<string, decimal>)>();

            // Main
            results.Add(await ExecuteRange(request.From, request.To, "Main"));

            // Comparison
            if (request.ComparisonRanges != null)
            {
                foreach (var range in request.ComparisonRanges)
                {
                    results.Add(await ExecuteRange(range.From, range.To, range.Label));
                }
            }

            // Align labels
            var allLabels = results
                .SelectMany(r => r.Item2.Keys)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            var seriesList = results.Select(r => new AnalyticsGrowthSeriesDTO
            {
                SeriesName = r.Name,
                Data = allLabels.Select(label => new AnalyticsGrowthPointDTO
                {
                    Label = label,
                    Value = r.Item2.ContainsKey(label) ? r.Item2[label] : 0
                }).ToList()
            }).ToList();

            return new AnalyticsGrowthDTO
            {
                Type = request.Type,
                Series = seriesList
            };
        }
    }
}
