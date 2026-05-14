using Domain.AcademicManagement.Entity;
using Domain.DomainExceptions;

namespace Domain.AcademicManagement.Aggregate
{
    public class Subject
    {
        #region Attributes
        private readonly List<DefaultLesson> defaultLessons = new();
        #endregion

        #region Properties
        public Guid SubjectID { get; private set; }
        public string Code { get; private set; }
        public string Name { get; private set; }
        public bool IsActive { get; private set; }

        public IReadOnlyCollection<DefaultLesson> DefaultLessons
        {
            get { return defaultLessons.AsReadOnly(); }
        }
        #endregion

        protected Subject() { }

        public Subject(
            Guid subjectId,
            string code,
            string name,
            Guid gradeId)
        {
            if (subjectId == Guid.Empty)
                throw new DomainException(
                    "Subject ID is required");

            if (string.IsNullOrWhiteSpace(code))
                throw new DomainException(
                    "Subject code is required");

            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException(
                    "Subject name is required");

            SubjectID = subjectId;
            Code = code.Trim();
            Name = name.Trim();
            IsActive = true;
        }

        #region Methods
        #endregion
    }
}

