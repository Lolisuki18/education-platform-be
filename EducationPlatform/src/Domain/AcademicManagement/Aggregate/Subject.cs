using Domain.AcademicManagement.Entity;
using Domain.Exceptions;

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

        // EF Core calls this constructor and then fills the properties
#pragma warning disable CS8618
        protected Subject() { }
#pragma warning restore CS8618

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
        public void Update(string code, string name)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new DomainException("Subject code is required");
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException("Subject name is required");

            Code = code.Trim();
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

