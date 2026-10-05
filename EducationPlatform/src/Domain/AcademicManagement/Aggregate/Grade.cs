using Domain.Exceptions;

namespace Domain.AcademicManagement.Aggregate
{
    public class Grade
    {
        #region Attributes
        #endregion

        #region Properties
        public Guid GradeID { get; private set; }
        public string Name { get; private set; }
        public bool IsActive { get; private set; }
        #endregion

        // EF Core calls this constructor and then fills the properties
#pragma warning disable CS8618
        protected Grade() { }
#pragma warning restore CS8618

        public Grade(
            Guid gradeId,
            string name)
        {
            if (gradeId == Guid.Empty)
                throw new DomainException(
                    "Grade ID is required");

            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException(
                    "Grade name is required");

            GradeID = gradeId;
            Name = name.Trim();
            IsActive = true;
        }

        #region Methods
        public void Update(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException("Grade name is required");

            Name = name.Trim();
        }

        public void Activate()
        {
            IsActive = true;
        }

        public void Deactivate()
        {
            IsActive = false;
        }
        #endregion
    }
}

