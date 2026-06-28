using Domain.DomainExceptions;

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

        protected Grade() { }

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
        #endregion
    }
}

