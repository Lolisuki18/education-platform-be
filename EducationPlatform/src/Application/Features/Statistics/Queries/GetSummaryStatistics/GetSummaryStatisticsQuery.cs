using Application.Features.Academic.Queries.GetGrades;
using Application.Features.Academic.Queries.GetSubjects;
using Application.Features.Statistics.Queries.GetSummaryStatistic;
using Application.Results;
using MediatR;
using Application.Interface;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

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
        private readonly IApplicationDBContext _context;
        private readonly IMapper _mapper;
        private readonly IMediator _mediator;

        public GetSummaryStatisticsQueryHandler(IApplicationDBContext context, IMapper mapper, IMediator mediator)
        {
            _context = context;
            _mapper = mapper;
            _mediator = mediator;
        }

        public async Task<SummaryStatisticsResult> Handle(GetSummaryStatisticsQuery request, CancellationToken cancellationToken)
        {
            // Change to DateTime.MinValue and DateTime.MaxValue so that if it happens to be null, it still scans the entire database
            var from = request.From ?? DateTime.MinValue;
            var to = request.To ?? DateTime.MaxValue;

            var grades = await _context.Grades
                .AsNoTracking()
                .ProjectTo<GradeDTO>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);

            var subjects = await _context.Subjects
                .AsNoTracking()
                .ProjectTo<SubjectDTO>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);

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
