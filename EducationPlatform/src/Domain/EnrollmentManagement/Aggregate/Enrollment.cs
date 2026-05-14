using Domain.CourseManagement.Aggregate;
using Domain.DomainExceptions;
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

        public CourseProgress CourseProgress { get; private set; }
        public Course Course { get; private set; }
        #endregion

        protected Enrollment() { }

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
            EnrolledAt = enrolledAt ?? DateTime.Now;
            StudentID = studentId;
            CourseID = courseId;

            CourseProgress = new CourseProgress(Guid.NewGuid(), enrollmentId);
        }

        #region Methods
        public void CompleteEnrollment(DateTime? completedAt)
        {
            CompletedAt = completedAt ?? DateTime.Now;
        }
        #endregion
    }
}

