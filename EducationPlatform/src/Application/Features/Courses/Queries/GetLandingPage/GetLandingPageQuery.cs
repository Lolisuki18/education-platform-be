using Application.Features.Academic.Queries.GetGrades;
using Application.Features.Academic.Queries.GetSubjects;
using Application.Results;
using MediatR;
using Application.Interface;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Courses.Queries.GetLandingPage
{
    public class GetLandingPageQuery : IRequest<LandingPageResult>
    {
        public string? Title { get; set; }
        public string? GradeName { get; set; }
        public string? SubjectName { get; set; }
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class LandingPageResult
    {
        public IEnumerable<CourseDTO> Courses { get; set; } = new List<CourseDTO>();
        public IEnumerable<GradeDTO> Grades { get; set; } = new List<GradeDTO>();
        public IEnumerable<SubjectDTO> Subjects { get; set; } = new List<SubjectDTO>();
    }

    public class GetLandingPageQueryHandler : IRequestHandler<GetLandingPageQuery, LandingPageResult>
    {
        private readonly IApplicationDBContext _context;
        private readonly IMapper _mapper;

        public GetLandingPageQueryHandler(IApplicationDBContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }

        public async Task<LandingPageResult> Handle(GetLandingPageQuery request, CancellationToken cancellationToken)
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

            var courses = await coursesQuery
                .OrderByDescending(c => c.PublishedAt)
                .Skip((request.PageIndex - 1) * request.PageSize)
                .Take(request.PageSize)
                .ProjectTo<CourseDTO>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);

            var grades = await _context.Grades
                .AsNoTracking()
                .ProjectTo<GradeDTO>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);

            var subjects = await _context.Subjects
                .AsNoTracking()
                .ProjectTo<SubjectDTO>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);

            return new LandingPageResult
            {
                Courses = courses,
                Grades = grades,
                Subjects = subjects
            };
        }
    }
}
