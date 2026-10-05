using Domain.CourseManagement.Enum;
using Domain.Exceptions;

namespace Domain.CourseManagement.ValueObject
{
    public sealed class QuizAnswer
    {
        public QuizType Type { get; }
        public IReadOnlyCollection<string> CorrectAnswers { get; }
        public IReadOnlyCollection<string>? Options { get; }

        // EF Core calls this constructor and then fills the properties
#pragma warning disable CS8618
        private QuizAnswer() { }
#pragma warning restore CS8618

        private QuizAnswer(QuizType type, IEnumerable<string> correctAnswers, IEnumerable<string>? options = null)
        {
            var answers = correctAnswers
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Select(a => a.Trim())
                .ToList();

            if (!answers.Any())
                throw new DomainException("At least one correct answer is required");

            Type = type;
            CorrectAnswers = answers.AsReadOnly();
            Options = options?.ToList().AsReadOnly();
        }

        public static QuizAnswer SingleChoice(string correctAnswer, IEnumerable<string>? options = null)
        {
            return new QuizAnswer(QuizType.SingleChoice, new[] { correctAnswer }, options);
        }

        public static QuizAnswer MultipleChoice(IEnumerable<string> correctAnswers, IEnumerable<string>? options = null)
        {
            return new QuizAnswer(QuizType.MultipleChoice, correctAnswers, options);
        }

        public static QuizAnswer TrueFalse(bool value)
        {
            return new QuizAnswer(QuizType.TrueFalse, new[] { value.ToString() });
        }
    }
}

