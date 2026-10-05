using Domain.CourseManagement.Enum;
using Domain.Exceptions;
using Domain.IdentityManagement.Aggregate;
using Domain.Common;
using Domain.CourseManagement.Events;

namespace Domain.CourseManagement.Aggregate
{
    public class Complaint : BaseEntity
    {
        #region Attributes
        #endregion

        #region Properties
        public Guid ComplaintID { get; private set; }
        public string Reason { get; private set; } = string.Empty;
        public string? EvidenceImagePath { get; private set; }
        public ComplaintStatus Status { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? ReviewedAt { get; private set; }
        public string? AdminNote { get; private set; }

        public Guid CourseID { get; private set; }
        public Guid StudentID { get; private set; }

        public Course Course { get; private set; } = null!;
        public User User { get; private set; } = null!;
        #endregion

        // EF Core calls this constructor and then fills the properties
#pragma warning disable CS8618
        protected Complaint() { }
#pragma warning restore CS8618

        public Complaint(
            Guid complaintId,
            Guid courseId,
            Guid studentId,
            string reason,
            string? evidenceImagePath)
        {
            if (complaintId == Guid.Empty)
                throw new DomainException("Complaint ID cannot be empty");

            if (courseId == Guid.Empty)
                throw new DomainException("Course ID cannot be empty");

            if (studentId == Guid.Empty)
                throw new DomainException("Student ID cannot be empty");

            if (string.IsNullOrWhiteSpace(reason))
                throw new DomainException("Complaint reason is required");

            ComplaintID = complaintId;
            CourseID = courseId;
            StudentID = studentId;
            Reason = reason.Trim();
            EvidenceImagePath = evidenceImagePath;
            Status = ComplaintStatus.Pending;
            CreatedAt = DateTime.UtcNow;
        }

        #region Methods
        public void Approve(string? adminNote)
        {
            if (Status != ComplaintStatus.Pending)
                throw new DomainException("Only pending complaints can be approved.");

            Status = ComplaintStatus.Approved;
            AdminNote = adminNote;
            ReviewedAt = DateTime.UtcNow;

            AddDomainEvent(new ComplaintApprovedEvent(ComplaintID, CourseID));
        }

        public void Reject(string? adminNote)
        {
            Status = ComplaintStatus.Rejected;
            AdminNote = adminNote;
            ReviewedAt = DateTime.UtcNow;
        }
        #endregion
    }
}

