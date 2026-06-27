using Domain.CourseManagement.Entity;
using Domain.DomainExceptions;

namespace Domain.EnrollmentManagement.Entity
{
    public class ChapterProgress
    {
        #region Attributes
        private readonly List<LessonProgress> lessonProgresses = new();
        #endregion

        #region Properties
        public Guid ChapterProgressID { get; private set; }
        public bool IsCompleted { get; private set; }

        public Guid CourseProgressID { get; private set; }
        public Guid ChapterID { get; private set; }

        public IReadOnlyCollection<LessonProgress> LessonProgresses
        {
            get { return lessonProgresses.AsReadOnly(); }
        }

        public Chapter Chapter { get; private set; }
        #endregion

        protected ChapterProgress() { }

        public ChapterProgress(Guid chapterProgressId, Guid courseProgressId, Guid chapterId)
        {
            if (chapterProgressId == Guid.Empty)
                throw new DomainException("Chapter progress ID cannot be empty");
            if (courseProgressId == Guid.Empty)
                throw new DomainException("Course progress ID cannot be empty");
            if (chapterId == Guid.Empty)
                throw new DomainException("Chapter ID cannot be empty");

            ChapterProgressID = chapterProgressId;
            CourseProgressID = courseProgressId;
            ChapterID = chapterId;
        }

        public LessonProgress AddLessonProgress(Guid lessonId)
        {
            var lessonProgress = new LessonProgress(Guid.NewGuid(), ChapterProgressID, lessonId);
            lessonProgresses.Add(lessonProgress);
            return lessonProgress;
        }

        public void RecalculateCompletion()
        {
            if (!lessonProgresses.Any())
            {
                IsCompleted = false;
                return;
            }

            foreach (var lesson in lessonProgresses)
            {
                lesson.RecalculateCompletion();
            }

            IsCompleted = lessonProgresses.All(l => l.IsCompleted);
        }
    }

}

