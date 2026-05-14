using Domain.CourseManagement.Entity;
using Domain.DomainExceptions;

namespace Domain.CourseManagement.Aggregate
{
    public class Policy
    {
        #region Attributes
        private readonly List<PolicyRule> rolicyRules = new();
        #endregion

        #region Properties
        public Guid PolicyID { get; private set; }
        public string Name { get; private set; }
        public bool IsActive { get; private set; }

        public IReadOnlyCollection<PolicyRule> PolicyRules
        {
            get { return rolicyRules.AsReadOnly(); }
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

