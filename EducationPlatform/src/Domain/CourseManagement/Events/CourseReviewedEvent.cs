using Domain.Common;
using Domain.CourseManagement.Enum;

namespace Domain.CourseManagement.Events
{
    public class CourseReviewedEvent : IDomainEvent
    {
        public Guid CourseID { get; }
        public string CourseTitle { get; }
        public CourseStatus Outcome { get; }   // Published or Rejected
        public Guid ReviewedByAdminID { get; }
        public Guid TeacherID { get; }         // Recipient of the notification
        public DateTime ReviewedAt { get; }

        public CourseReviewedEvent(
            Guid courseId,
            string courseTitle,
            CourseStatus outcome,
            Guid reviewedByAdminId,
            Guid teacherId)
        {
            CourseID = courseId;
            CourseTitle = courseTitle;
            Outcome = outcome;
            ReviewedByAdminID = reviewedByAdminId;
            TeacherID = teacherId;
            ReviewedAt = DateTime.UtcNow;
        }
    }
}
