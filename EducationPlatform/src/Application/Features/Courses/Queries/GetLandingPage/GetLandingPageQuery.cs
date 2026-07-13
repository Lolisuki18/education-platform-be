using Application.Results;
using MediatR;
using Application.Interface;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Courses.Queries.GetLandingPage
{
    public class GetLandingPageQuery : IRequest<PagedResult<CourseDTO>>
    {
        public string? Title { get; set; }
        public string? GradeName { get; set; }
        public string? SubjectName { get; set; }
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class GetLandingPageQueryHandler : IRequestHandler<GetLandingPageQuery, PagedResult<CourseDTO>>
    {
        private readonly IApplicationDBContext _context;
        private readonly IMapper _mapper;

        public GetLandingPageQueryHandler(IApplicationDBContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<PagedResult<CourseDTO>> Handle(GetLandingPageQuery request, CancellationToken cancellationToken)
        {
            var coursesQuery = _context.Courses
                .AsNoTracking()
                .Where(c => c.Status == Domain.CourseManagement.Enum.CourseStatus.Published);

            if (!string.IsNullOrWhiteSpace(request.Title))
                coursesQuery = coursesQuery.Where(c => c.Title.Contains(request.Title));

            if (!string.IsNullOrWhiteSpace(request.GradeName))
                coursesQuery = coursesQuery.Where(c => c.Grade.Name == request.GradeName);

            if (!string.IsNullOrWhiteSpace(request.SubjectName))
                coursesQuery = coursesQuery.Where(c => c.Subject.Name == request.SubjectName);

            var totalCount = await coursesQuery.CountAsync(cancellationToken);

            var courses = await coursesQuery
                .OrderByDescending(c => c.PublishedAt)
                .Skip((request.PageIndex - 1) * request.PageSize)
                .Take(request.PageSize)
                .ProjectTo<CourseDTO>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);

            return new PagedResult<CourseDTO>
            {
                Items = courses.AsReadOnly(),
                PageIndex = request.PageIndex,
                PageSize = request.PageSize,
                TotalItems = totalCount
            };
        }
    }
}
