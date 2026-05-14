using Domain.CourseManagement.Entity;
using Domain.DomainExceptions;

namespace Domain.EnrollmentManagement.Entity
{
    public class QuizProgress
    {
        #region Attributes
        #endregion

        #region Properties
        public Guid QuizProgressID { get; private set; }
        public bool IsCorrect { get; private set; }
        public int AttemptCount { get; private set; }
        public DateTime? LastAttemptAt { get; private set; }

        public Guid LessonProgressID { get; private set; }
        public Guid QuizID { get; private set; }

        public Quiz Quiz { get; private set; }
        #endregion

        protected QuizProgress() { }

        public QuizProgress(
            Guid quizProgressID,
            Guid lessonProgressId,
            Guid quizId)
        {
            if (quizProgressID == Guid.Empty)
                throw new DomainException(
                    "Quiz Progress ID cannot be empty");

            if (lessonProgressId == Guid.Empty)
                throw new DomainException(
                    "Lesson progress ID cannot be empty");

            if (quizId == Guid.Empty)
                throw new DomainException(
                    "Quiz ID cannot be empty");

            QuizProgressID = quizProgressID;
            AttemptCount = 0;
            LessonProgressID = lessonProgressId;
            QuizID = quizId;
        }

        #region Methods
        public void RegisterAttempt(bool isCorrect)
        {
            AttemptCount++;
            LastAttemptAt = DateTime.UtcNow;

            if (isCorrect)
                IsCorrect = true;
        }
        #endregion
    }
}

