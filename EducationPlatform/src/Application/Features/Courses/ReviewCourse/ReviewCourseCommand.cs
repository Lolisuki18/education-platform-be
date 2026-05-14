using MediatR;

namespace Application.Features.Courses.ReviewCourse
{
    /// <summary>
    /// Command: Admin reviews a course submission.
    /// Carries the pure data needed to drive the domain method Course.ReviewCourse(…).
    /// No business logic lives here — only intent + data.
    /// </summary>
    public class ReviewCourseCommand : IRequest
    {
        // ----- Course to review -----
        public Guid CourseID { get; set; }

        // ----- Policy violations (empty = approve) -----
        public List<Guid>? ViolatedPolicyIDs { get; set; }

        // ----- Chapter-level notes -----
        public List<ViolatedChapterItem>? ViolatedChapters { get; set; }

        // ----- Optional admin note -----
        public string? AdminNote { get; set; }
    }

    public class ViolatedChapterItem
    {
        public Guid ViolatedChapterId { get; set; }
        public string AdminNote { get; set; } = string.Empty;
    }
}
