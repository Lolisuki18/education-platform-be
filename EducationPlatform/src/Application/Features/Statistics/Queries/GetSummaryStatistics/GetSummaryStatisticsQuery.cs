using Application.Features.Academic.Queries.GetGrades;
using Application.Features.Academic.Queries.GetSubjects;
using Application.Features.Statistics.Queries.GetSummaryStatistic;
using Application.Results;
using MediatR;

namespace Application.Features.Statistics.Queries.GetSummaryStatistics
{
    public class GetSummaryStatisticsQuery : IRequest<SummaryStatisticsResult>
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
    }

    public class SummaryStatisticsResult
    {
        public IEnumerable<GradeDTO> Grades { get; set; } = new List<GradeDTO>();
        public IEnumerable<SubjectDTO> Subjects { get; set; } = new List<SubjectDTO>();
        public SummaryStatisticDTO Summary { get; set; } = new();
    }

    public class GetSummaryStatisticsQueryHandler : IRequestHandler<GetSummaryStatisticsQuery, SummaryStatisticsResult>
    {
        private readonly IMediator _mediator;

        public GetSummaryStatisticsQueryHandler(IMediator mediator)
        {
            _mediator = mediator;
        }

        public async Task<SummaryStatisticsResult> Handle(GetSummaryStatisticsQuery request, CancellationToken cancellationToken)
        {
            var from = request.From ?? DateTime.Now.AddMonths(-1);
            var to = request.To ?? DateTime.Now;

            var grades = await _mediator.Send(new GetGradesQuery(), cancellationToken);
            var subjects = await _mediator.Send(new GetSubjectsQuery(), cancellationToken);
            var summary = await _mediator.Send(new GetSummaryStatisticQuery
            {
                From = from,
                To = to
            }, cancellationToken);

            return new SummaryStatisticsResult
            {
                Grades = grades,
                Subjects = subjects,
                Summary = summary
            };
        }
    }
}
