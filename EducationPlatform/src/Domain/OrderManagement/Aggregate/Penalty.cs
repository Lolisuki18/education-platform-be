using Domain.CourseManagement.Aggregate;
using Domain.Exceptions;
using Domain.IdentityManagement.Aggregate;

namespace Domain.OrderManagement.Aggregate
{
    public class Penalty
    {
        #region Properties
        public Guid PenaltyID { get; private set; }
        public decimal PenaltyAmount { get; private set; }
        public string Reason { get; private set; } = string.Empty;
        public DateTime CreatedAt { get; private set; }

        public Guid TeacherID { get; private set; }
        public Guid CourseID { get; private set; }

        public User Teacher { get; private set; } = null!;
        public Course Course { get; private set; } = null!;
        #endregion

        // EF Core calls this constructor and then fills the properties
#pragma warning disable CS8618
        protected Penalty() { }
#pragma warning restore CS8618

        public Penalty(
            Guid penaltyId,
            Guid teacherId,
            Guid courseId,
            decimal penaltyAmount,
            string reason)
        {
            if (penaltyId == Guid.Empty)
                throw new DomainException("Penalty ID cannot be empty");

            if (teacherId == Guid.Empty)
                throw new DomainException("Teacher ID cannot be empty");

            if (penaltyAmount <= 0)
                throw new DomainException("Penalty amount must be greater than zero");

            if (string.IsNullOrWhiteSpace(reason))
                throw new DomainException("Penalty reason is required");

            PenaltyID = penaltyId;
            TeacherID = teacherId;
            CourseID = courseId;
            PenaltyAmount = penaltyAmount;
            Reason = reason.Trim();
            CreatedAt = DateTime.UtcNow;
        }

        #region Methods
        #endregion
    }
}

