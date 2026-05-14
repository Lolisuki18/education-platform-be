using Application.Results;
using MediatR;
using Domain.Common.Interfaces;
using Domain.CourseManagement.Aggregate;
using Domain.EnrollmentManagement.Aggregate;
using Application.Enums;
using System.Linq;

namespace Application.Features.Statistics.Queries.GetAnalyticsDemandAndSupply
{
    public class GetAnalyticsDemandAndSupplyQuery : IRequest<AnalyticsGrowthDTO>
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public AnalyticGroupDate GroupBy { get; set; }
        public Guid? CourseGradeId { get; set; }
        public Guid? CourseSubjectId { get; set; }
        public Guid? EnrollmentGradeId { get; set; }
        public Guid? EnrollmentSubjectId { get; set; }
    }

    public class GetAnalyticsDemandAndSupplyQueryHandler : IRequestHandler<GetAnalyticsDemandAndSupplyQuery, AnalyticsGrowthDTO>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetAnalyticsDemandAndSupplyQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<AnalyticsGrowthDTO> Handle(GetAnalyticsDemandAndSupplyQuery request, CancellationToken cancellationToken)
        {
            var courseRepo = _unitOfWork.GetRepository<ICourseRepository>();
            var enrollmentRepo = _unitOfWork.GetRepository<IEnrollmentRepository>();

            var courseRaw = await courseRepo.AnalyticsGrowth(
                request.From, request.To, request.GroupBy.ToString(),
                request.CourseGradeId, request.CourseSubjectId);

            var enrollmentRaw = await enrollmentRepo.AnalyticsGrowth(
                request.From, request.To, request.GroupBy.ToString(),
                request.EnrollmentGradeId, request.EnrollmentSubjectId);

            var courseDict = courseRaw.FirstOrDefault().Value?
                .GroupBy(x => x.Label)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Value)) ?? new Dictionary<string, decimal>();

            var enrollmentDict = enrollmentRaw.FirstOrDefault().Value?
                .GroupBy(x => x.Label)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Value)) ?? new Dictionary<string, decimal>();

            var allLabels = courseDict.Keys
                .Union(enrollmentDict.Keys)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            var seriesList = new List<AnalyticsGrowthSeriesDTO>
            {
                new AnalyticsGrowthSeriesDTO
                {
                    SeriesName = "Supply (Courses)",
                    Data = allLabels.Select(label => new AnalyticsGrowthPointDTO
                    {
                        Label = label,
                        Value = courseDict.ContainsKey(label) ? courseDict[label] : 0
                    }).ToList()
                },
                new AnalyticsGrowthSeriesDTO
                {
                    SeriesName = "Demand (Enrollments)",
                    Data = allLabels.Select(label => new AnalyticsGrowthPointDTO
                    {
                        Label = label,
                        Value = enrollmentDict.ContainsKey(label) ? enrollmentDict[label] : 0
                    }).ToList()
                }
            };

            return new AnalyticsGrowthDTO
            {
                Type = AnalyticsGrowthType.Enrollment,
                Series = seriesList
            };
        }
    }
}
