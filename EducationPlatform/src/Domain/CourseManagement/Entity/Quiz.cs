using Domain.CourseManagement.Enum;
using Domain.CourseManagement.ValueObject;
using Domain.Exceptions;

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

        public QuizAnswer Answer { get; private set; } = null!;
        #endregion

        // EF Core calls this constructor and then fills the properties
#pragma warning disable CS8618
        protected Quiz() { }
#pragma warning restore CS8618

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
            // A true/false question is answered by its value alone: it has no list of answers and no options
            if (type == QuizType.TrueFalse)
            {
                Answer = QuizAnswer.TrueFalse(trueFalseValue ?? false);
                return;
            }

            if (answers == null || !answers.Any() || options == null || !options.Any())
                throw new DomainException(
                    "Quiz must have answer and options");

            // A correct answer that is not one of the options could never be picked, so the quiz could never be passed
            var mustBeOptions = type == QuizType.SingleChoice ? new[] { answers.First() } : answers.ToArray();
            if (type is QuizType.SingleChoice or QuizType.MultipleChoice &&
                !mustBeOptions.All(a => options.Any(o => string.Equals(o?.Trim(), a?.Trim(), StringComparison.OrdinalIgnoreCase))))
                throw new DomainException("Every correct answer must be one of the options");

            Answer = type switch
            {
                QuizType.SingleChoice => QuizAnswer.SingleChoice(answers.First(), options),
                QuizType.MultipleChoice => QuizAnswer.MultipleChoice(answers, options),

                _ => throw new DomainException("Unsupported quiz type")
            };
        }
        #endregion
    }
}

