using Domain.CourseManagement.Enum;
using Domain.CourseManagement.ValueObject;
using Domain.DomainExceptions;

namespace Domain.CourseManagement.Entity
{
    public class Quiz
    {
        #region Attributes
        #endregion

        #region Properties
        public Guid QuizID { get; private set; }
        public string Question { get; private set; }
        public string? Note { get; private set; }

        public Guid LessonID { get; private set; }

        public QuizAnswer Answer { get; private set; }
        #endregion

        protected Quiz() { }

        public Quiz(
            Guid quizId,
            string question,
            string? note,
            Guid lessonId)
        {
            if (quizId == Guid.Empty)
                throw new DomainException(
                    "Quiz ID cannot be empty");

            if (lessonId == Guid.Empty)
                throw new DomainException(
                    "Lesson ID cannot be empty");

            if (string.IsNullOrWhiteSpace(question))
                throw new DomainException(
                    "Question is required");

            QuizID = quizId;
            Question = question.Trim();
            Note = note?.Trim();
            LessonID = lessonId;
        }

        #region Methods
        public void AddAnswer(
            QuizType type,
            IEnumerable<string>? answers = null,
            IEnumerable<string>? options = null,
            bool? trueFalseValue = null)
        {
            if (answers == null || !answers.Any() || options == null || !options.Any())
                throw new DomainException(
                    "Quiz must have answer and options");

            Answer = type switch
            {
                QuizType.SingleChoice => QuizAnswer.SingleChoice(answers?.FirstOrDefault() ?? "", options),
                QuizType.MultipleChoice => QuizAnswer.MultipleChoice(answers ?? new List<string>(), options),
                QuizType.TrueFalse => QuizAnswer.TrueFalse(trueFalseValue ?? false),

                _ => throw new DomainException("Unsupported quiz type")
            };
        }
        #endregion
    }
}

