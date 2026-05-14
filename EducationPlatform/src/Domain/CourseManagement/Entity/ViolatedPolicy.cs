using Domain.CourseManagement.Aggregate;
using Domain.DomainExceptions;

namespace Domain.CourseManagement.Entity
{
    public class ViolatedPolicy
    {
        #region Attributes
        #endregion

        #region Properties
        public Guid ViolatedPolicyID { get; private set; }

        public Guid PolicyID { get; private set; }
        public Guid CourseID { get; private set; }

        public Policy Policy { get; private set; }
        #endregion

        protected ViolatedPolicy() { }

        public ViolatedPolicy(
            Guid violatedPolicyId,
            Guid policyId,
            Guid courseId)
        {
            if (violatedPolicyId == Guid.Empty)
                throw new DomainException(
                    "Violated policy ID is required");

            if (policyId == Guid.Empty)
                throw new DomainException(
                    "Policy ID is required");

            if (courseId == Guid.Empty)
                throw new DomainException(
                    "Course ID is required");

            ViolatedPolicyID = violatedPolicyId;
            PolicyID = policyId;
            CourseID = courseId;
        }

        #region Methods
        #endregion
    }
}

