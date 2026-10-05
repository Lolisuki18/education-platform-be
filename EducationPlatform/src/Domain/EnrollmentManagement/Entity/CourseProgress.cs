using Domain.Exceptions;

namespace Domain.EnrollmentManagement.Entity
{
    public class CourseProgress
    {
        #region Attributes
        private readonly List<ChapterProgress> chapterProgresses = new();
        #endregion

        #region Properties
        public Guid CourseProgressID { get; private set; }
        public decimal CompletionRate { get; private set; }
        public bool IsCompleted { get; private set; }

        public Guid EnrollmentID { get; private set; }

        public IReadOnlyCollection<ChapterProgress> ChapterProgresses => chapterProgresses.AsReadOnly();
        #endregion

        protected CourseProgress() { }

        public CourseProgress(Guid courseProgressId, Guid enrollmentId)
        {
            if (courseProgressId == Guid.Empty)
                throw new DomainException("Course progress ID cannot be empty");

            if (enrollmentId == Guid.Empty)
                throw new DomainException("Enrollment ID cannot be empty");

            CourseProgressID = courseProgressId;
            CompletionRate = 0;
            IsCompleted = false;
            EnrollmentID = enrollmentId;
        }

        #region Methods
        public ChapterProgress AddChapterProgress(Guid chapterId)
        {
            var chapterProgress = new ChapterProgress(Guid.NewGuid(), CourseProgressID, chapterId);
            chapterProgresses.Add(chapterProgress);
            return chapterProgress;
        }

        public void RecalculateCompletion()
        {
            if (!chapterProgresses.Any())
            {
                CompletionRate = 0;
                IsCompleted = false;
                return;
            }

            foreach (var chapter in chapterProgresses)
            {
                chapter.RecalculateCompletion();
            }

            var completedLessons = chapterProgresses.Sum(c => c.LessonProgresses.Count(l => l.IsCompleted));
            var totalLessons = chapterProgresses.Sum(c => c.LessonProgresses.Count);

            if (totalLessons == 0)
            {
                CompletionRate = 0;
                IsCompleted = false;
                return;
            }

            CompletionRate = Math.Round((decimal)completedLessons * 100 / totalLessons, 2);
            IsCompleted = completedLessons == totalLessons;
        }
        #endregion
    }
}

