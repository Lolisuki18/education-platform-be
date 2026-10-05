using Domain.Exceptions;

namespace Domain.CourseManagement.Entity
{
    public class PolicyRule
    {
        #region Attributes
        #endregion

        #region Properties
        public Guid PolicyRuleID { get; private set; }
        public string Code { get; private set; }
        public string Description { get; private set; }

        public Guid PolicyID { get; private set; }
        public bool IsActive { get; private set; } = true;
        #endregion

        // EF Core calls this constructor and then fills the properties
#pragma warning disable CS8618
        protected PolicyRule() { }
#pragma warning restore CS8618

        public PolicyRule(
            Guid policyRuleId,
            string code,
            string description,
            Guid policyId)
        {
            if (policyRuleId == Guid.Empty)
                throw new DomainException(
                    "Policy rule ID is required");

            if (policyId == Guid.Empty)
                throw new DomainException(
                    "Policy ID is required");

            if (string.IsNullOrWhiteSpace(code))
                throw new DomainException(
                    "Policy rule code is required");

            if (string.IsNullOrWhiteSpace(description))
                throw new DomainException(
                    "Policy rule description is required");

            PolicyRuleID = policyRuleId;
            Code = code;
            Description = description;
            PolicyID = policyId;
        }

        #region Methods
        #endregion
    }
}

