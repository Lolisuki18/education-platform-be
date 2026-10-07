using Domain.CourseManagement.Aggregate;
using Domain.Exceptions;
using Domain.EnrollmentManagement.Entity;
using Domain.EnrollmentManagement.Enum;

namespace Domain.EnrollmentManagement.Aggregate
{
    public class Enrollment
    {
        #region Attributes
        #endregion

        #region Properties
        public Guid EnrollmentID { get; private set; }
        public EnrollmentStatus Status { get; private set; }
        public DateTime EnrolledAt { get; private set; }
        public DateTime? CompletedAt { get; private set; }

        public Guid StudentID { get; private set; }
        public Guid CourseID { get; private set; }

        public CourseProgress CourseProgress { get; private set; } = null!;
        public Course Course { get; private set; } = null!;
        #endregion

        // EF Core calls this constructor and then fills the properties
#pragma warning disable CS8618
        protected Enrollment() { }
#pragma warning restore CS8618

        public Enrollment(
            Guid enrollmentId,
            Guid studentId,
            Guid courseId,
            DateTime? enrolledAt)
        {
            if (enrollmentId == Guid.Empty)
                throw new DomainException(
                    "Enrollment ID cannot be empty");

            if (studentId == Guid.Empty)
                throw new DomainException(
                    "Student ID cannot be empty");

            if (courseId == Guid.Empty)
                throw new DomainException(
                    "Course ID cannot be empty");

            EnrollmentID = enrollmentId;
            Status = EnrollmentStatus.Active;
            EnrolledAt = enrolledAt ?? DateTime.UtcNow;
            StudentID = studentId;
            CourseID = courseId;

            CourseProgress = new CourseProgress(Guid.NewGuid(), enrollmentId);
        }

        #region Methods
        public void CompleteEnrollment(DateTime? completedAt)
        {
            CompletedAt = completedAt ?? DateTime.UtcNow;

            // A revoked enrollment stays revoked; every other one is finished now (the admin dashboard counts this status)
            if (Status == EnrollmentStatus.Active)
                Status = EnrollmentStatus.Completed;
        }
        #endregion
    }
}

