using Domain.CourseManagement.Entity;
using Domain.Exceptions;

namespace Domain.CourseManagement.Aggregate
{
    public class Policy
    {
        #region Attributes
        private readonly List<PolicyRule> policyRules = new();
        #endregion

        #region Properties
        public Guid PolicyID { get; private set; }
        public string Name { get; private set; }
        public bool IsActive { get; private set; }

        public IReadOnlyCollection<PolicyRule> PolicyRules
        {
            get { return policyRules.AsReadOnly(); }
        }
        #endregion

        protected Policy() { }

        public Policy(
            Guid policyId,
            string name)
        {
            if (policyId == Guid.Empty)
                throw new DomainException(
                    "Policy ID cannot be empty");

            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException(
                    "Policy name is required");

            PolicyID = policyId;
            Name = name.Trim();
            IsActive = true;
        }

        #region Methods
        #endregion
    }
}

