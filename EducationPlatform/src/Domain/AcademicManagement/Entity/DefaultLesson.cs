using Domain.Exceptions;

namespace Domain.AcademicManagement.Entity
{
    public class DefaultLesson
    {
        #region Attributes
        #endregion

        #region Properties
        public Guid DefaultLessonID { get; private set; }
        public string Objectives { get; private set; }
        public string Description { get; private set; }
        public string Name { get; private set; }
        public bool IsActive { get; private set; }

        public Guid SubjectID { get; private set; }
        public Guid GradeID { get; private set; }
        #endregion

        // EF Core calls this constructor and then fills the properties
#pragma warning disable CS8618
        protected DefaultLesson() { }
#pragma warning restore CS8618

        public DefaultLesson(
            Guid defaultLessonId,
            string objectives,
            string description,
            string name,
            Guid gradeId,
            Guid subjectId)
        {
            if (defaultLessonId == Guid.Empty)
                throw new DomainException(
                    "Default lesson ID is required");

            if (string.IsNullOrWhiteSpace(objectives))
                throw new DomainException(
                    "Default lesson objectives is required");

            if (string.IsNullOrWhiteSpace(description))
                throw new DomainException(
                    "Default lesson description is required");

            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException(
                    "Default lesson name is required");

            if (subjectId == Guid.Empty)
                throw new DomainException(
                    "Subject ID is required");

            if (subjectId == Guid.Empty)
                throw new DomainException(
                    "Grade ID is required");

            DefaultLessonID = defaultLessonId;
            Objectives = objectives;
            Description = description.Trim();
            Name = name.Trim();
            IsActive = true;
            SubjectID = subjectId;
            GradeID = gradeId;
        }

        #region Methods
        #endregion
    }
}

