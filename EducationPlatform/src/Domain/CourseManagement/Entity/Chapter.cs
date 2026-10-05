using Domain.Exceptions;

namespace Domain.CourseManagement.Entity
{
    public class Chapter
    {
        #region Attributes
        private readonly List<Lesson> lessons = new();
        #endregion

        #region Properties
        public Guid ChapterID { get; private set; }
        public string Title { get; private set; }
        public string? Description { get; private set; }
        public int Order { get; private set; }
        public bool IsViolated { get; private set; }
        public string? AdminNote { get; private set; }

        public Guid CourseID { get; private set; }

        public IReadOnlyCollection<Lesson> Lessons
        {
            get { return lessons.AsReadOnly(); }
        }
        #endregion

        protected Chapter() { }

        public Chapter(
            Guid chapterId,
            string? description,
            string title,
            int order,
            Guid courseId)
        {
            if (chapterId == Guid.Empty)
                throw new DomainException("Chapter ID cannot be empty");

            if (string.IsNullOrWhiteSpace(title))
                throw new DomainException("Chapter title is required");

            if (order <= 0)
                throw new DomainException("Chapter order must be greater than zero");

            ChapterID = chapterId;
            Title = title.Trim();
            Description = description?.Trim();
            Order = order;
            CourseID = courseId;
        }

        #region Methods
        public Lesson AddLesson(
            string title,
            string objectives,
            string description,
            string videoUrl)
        {
            var lesson = new Lesson(
                Guid.NewGuid(),
                title,
                objectives,
                description,
                videoUrl,
                lessons.Count + 1,
                ChapterID);

            lessons.Add(lesson);

            return lesson;
        }

        public void NoteAsViolated(string adminNote)
        {
            if (string.IsNullOrEmpty(adminNote))
                throw new DomainException($"All violated lesson must contain a note");

            IsViolated = true;
            AdminNote = adminNote.Trim();
        }

        public void NoteAsNonViolated()
        {
            IsViolated = false;
            AdminNote = string.Empty;
        }
        #endregion
    }
}

