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
    ///
    /// Caller context is optional — when null the handler treats the request
    /// as an anonymous/public discovery call.
    /// </summary>
    public class GetCourseDetailQuery : IRequest<CourseDetailDTO>
    {
        public Guid CourseID { get; set; }

        // Optional caller context — set by Controller when user is authenticated
        public Guid? CallerId { get; set; }
        public string? CallerRole { get; set; }
    }
}
