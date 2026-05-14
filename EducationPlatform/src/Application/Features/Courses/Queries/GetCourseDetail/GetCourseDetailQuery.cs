using Application.Results;
using MediatR;

namespace Application.Features.Courses.Queries.GetCourseDetail
{
    /// <summary>
    /// Query: Fetch a single course's full detail.
    ///
    /// Visibility rules (enforced in Handler):
    ///   - Unauthenticated / Student : no Chapters (public preview only)
    ///   - Teacher (owner)           : full detail including Chapters
    ///   - Admin                     : full detail + AdminNote + ViolatedPolicies
    /// </summary>
    public class GetCourseDetailQuery : IRequest<CourseDetailDTO>
    {
        public Guid CourseID { get; set; }
    }
}
