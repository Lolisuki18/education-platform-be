using Application.Results;
using MediatR;

namespace Application.Features.Courses.Queries.GetCourseDetail
{
    public class GetCourseDetailQuery : IRequest<CourseDetailDTO>
    {
        public Guid CourseID { get; set; }
    }
}
