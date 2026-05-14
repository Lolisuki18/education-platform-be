using Application.Features.Academic.Queries.GetGrades;
using Application.Features.Academic.Queries.GetSubjects;
using Application.Results;
using MediatR;

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
        private readonly IMediator _mediator;

        public GetLandingPageQueryHandler(IMediator mediator)
        {
            _mediator = mediator;
        }

        public async Task<LandingPageResult> Handle(GetLandingPageQuery request, CancellationToken cancellationToken)
        {
            var courses = await _mediator.Send(new GetCourses.GetCoursesQuery
            {
                Title = request.Title,
                GradeName = request.GradeName,
                SubjectName = request.SubjectName,
                PageIndex = request.PageIndex,
                PageSize = request.PageSize
            }, cancellationToken);

            var grades = await _mediator.Send(new GetGradesQuery(), cancellationToken);
            var subjects = await _mediator.Send(new GetSubjectsQuery(), cancellationToken);

            return new LandingPageResult
            {
                Courses = courses,
                Grades = grades,
                Subjects = subjects
            };
        }
    }
}
