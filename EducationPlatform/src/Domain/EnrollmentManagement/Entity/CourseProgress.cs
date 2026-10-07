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

        /// <summary>
        /// Progress is measured against every lesson the course has, not only the ones the student has touched:
        /// finishing the first of ten lessons is 10%, not 100%.
        /// </summary>
        public void RecalculateCompletion(IReadOnlyCollection<LessonOutline> outline)
        {
            foreach (var chapter in chapterProgresses)
            {
                chapter.RecalculateCompletion(outline);
            }

            if (outline.Count == 0)
            {
                CompletionRate = 0;
                IsCompleted = false;
                return;
            }

            var completedLessons = outline.Count(o => chapterProgresses.Any(c =>
                c.LessonProgresses.Any(l => l.LessonID == o.LessonID && l.IsCompleted)));

            CompletionRate = Math.Round((decimal)completedLessons * 100 / outline.Count, 2);
            IsCompleted = completedLessons == outline.Count;
        }
        #endregion
    }
}

