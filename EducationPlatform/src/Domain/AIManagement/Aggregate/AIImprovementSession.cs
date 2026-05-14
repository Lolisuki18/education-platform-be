using Domain.DomainExceptions;
using Domain.AIManagement.Enum;
using Domain.AIManagement.Entity;

namespace Domain.AIManagement.Aggregate
{
    public class AIImprovementSession
    {
        #region Attributes
        private readonly List<AIAssignment> aiAssignments = new List<AIAssignment>();
        #endregion

        #region Properties
        public Guid SessionID { get; private set; }
        public AIImprovementStatus Status { get; private set; }
        public string Insight { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? CompletedAt { get; private set; }

        public Guid StudentID { get; private set; }
        public Guid CourseID { get; private set; }
        public Guid EnrollmentID { get; private set; }

        public IReadOnlyCollection<AIAssignment> AIAssignments
        {
            get { return aiAssignments.AsReadOnly(); }
        }
        #endregion

        protected AIImprovementSession() { }

        public AIImprovementSession(
            Guid sessionId,
            string insight,
            Guid studentId,
            Guid courseId,
            Guid enrollmentId)
        {
            if (sessionId == Guid.Empty)
                throw new DomainException("Session ID cannot be empty");

            if (studentId == Guid.Empty)
                throw new DomainException("Student ID cannot be empty");

            if (courseId == Guid.Empty)
                throw new DomainException("Course ID cannot be empty");

            if (enrollmentId == Guid.Empty)
                throw new DomainException("Enrollment ID cannot be empty");

            SessionID = sessionId;
            Insight = insight;
            Status = AIImprovementStatus.Active;
            CreatedAt = DateTime.UtcNow;
            StudentID = studentId;
            CourseID = courseId;
            EnrollmentID = enrollmentId;
        }

        #region Methods
        #endregion
    }
}

