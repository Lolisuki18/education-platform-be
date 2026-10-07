using Domain.CourseManagement.Entity;
using Domain.Exceptions;

namespace Domain.EnrollmentManagement.Entity
{
    public class LessonProgress
    {
        #region Attributes
        private readonly List<QuizProgress> quizProgresses = new();
        #endregion

        #region Properties
        public Guid LessonProgressID { get; private set; }
        public bool IsCompleted { get; private set; }
        public DateTime? CompletedAt { get; private set; }

        public Guid ChapterProgressID { get; private set; }
        public Guid LessonID { get; private set; }

        public IReadOnlyCollection<QuizProgress> QuizProgresses
        {
            get { return quizProgresses.AsReadOnly(); }
        }

        public Lesson Lesson { get; private set; } = null!;
        #endregion

        // EF Core calls this constructor and then fills the properties
#pragma warning disable CS8618
        protected LessonProgress() { }
#pragma warning restore CS8618

        public LessonProgress(
            Guid lessonProgressID,
            Guid chapterProgressID,
            Guid lessonID)
        {
            if (lessonProgressID == Guid.Empty)
                throw new DomainException(
                    "Lesson progress ID cannot be empty");

            if (chapterProgressID == Guid.Empty)
                throw new DomainException(
                    "Course progress ID cannot be empty");

            if (lessonID == Guid.Empty)
                throw new DomainException(
                    "Lesson ID cannot be empty");

            LessonProgressID = lessonProgressID;
            IsCompleted = false;
            ChapterProgressID = chapterProgressID;
            LessonID = lessonID;
        }

        #region Methods
        public void MarkCompleted()
        {
            IsCompleted = true;
            CompletedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// A lesson with quizzes is finished once every one of them (not only the ones attempted so far) has been
        /// answered correctly. A lesson without quizzes is finished only when the student marks it so.
        /// </summary>
        public void RecalculateCompletion(int totalQuizzes)
        {
            if (IsCompleted) return;

            if (totalQuizzes > 0 && quizProgresses.Count(q => q.IsCorrect) >= totalQuizzes)
            {
                IsCompleted = true;
                CompletedAt = DateTime.UtcNow;
            }
        }

        public QuizProgress AddQuizProgress(Guid quizId)
        {
            var quizProgress = new QuizProgress(Guid.NewGuid(), LessonProgressID, quizId);
            quizProgresses.Add(quizProgress);
            return quizProgress;
        }
        #endregion
    }
}

