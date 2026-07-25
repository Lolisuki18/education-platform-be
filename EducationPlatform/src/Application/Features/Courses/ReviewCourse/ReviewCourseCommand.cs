using MediatR;

namespace Application.Features.Courses.ReviewCourse
{
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
