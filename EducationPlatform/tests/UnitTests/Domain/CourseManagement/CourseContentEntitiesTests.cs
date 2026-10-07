using Domain.CourseManagement.Entity;
using Domain.CourseManagement.Enum;
using Domain.CourseManagement.ValueObject;
using Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace UnitTests.DomainTests.CourseManagement
{
    public class QuizTests
    {
        private static Quiz NewQuiz() => new(Guid.NewGuid(), "  What is 2 + 2?  ", "  careful  ", Guid.NewGuid());

        [Fact]
        public void Constructor_TrimsTheQuestionAndTheNote()
        {
            var quiz = NewQuiz();

            quiz.Question.Should().Be("What is 2 + 2?");
            quiz.Note.Should().Be("careful");
        }

        [Fact]
        public void Constructor_NoteIsOptional()
        {
            new Quiz(Guid.NewGuid(), "Question", null, Guid.NewGuid()).Note.Should().BeNull();
        }

        [Fact]
        public void Constructor_RefusesEmptyIds()
        {
            ((Action)(() => new Quiz(Guid.Empty, "Q", null, Guid.NewGuid()))).Should().Throw<DomainException>().WithMessage("Quiz ID cannot be empty");
            ((Action)(() => new Quiz(Guid.NewGuid(), "Q", null, Guid.Empty))).Should().Throw<DomainException>().WithMessage("Lesson ID cannot be empty");
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_RefusesABlankQuestion(string question)
        {
            var act = () => new Quiz(Guid.NewGuid(), question, null, Guid.NewGuid());

            act.Should().Throw<DomainException>().WithMessage("Question is required");
        }

        [Fact]
        public void AddAnswer_SingleChoice_KeepsTheFirstAnswerAndTheOptions()
        {
            var quiz = NewQuiz();

            quiz.AddAnswer(QuizType.SingleChoice, new[] { "4", "5" }, new[] { "3", "4", "5" });

            quiz.Answer.Type.Should().Be(QuizType.SingleChoice);
            quiz.Answer.CorrectAnswers.Should().Equal("4");
            quiz.Answer.Options.Should().Equal("3", "4", "5");
        }

        [Fact]
        public void AddAnswer_MultipleChoice_KeepsEveryAnswer()
        {
            var quiz = NewQuiz();

            quiz.AddAnswer(QuizType.MultipleChoice, new[] { "2", "4" }, new[] { "1", "2", "3", "4" });

            quiz.Answer.Type.Should().Be(QuizType.MultipleChoice);
            quiz.Answer.CorrectAnswers.Should().Equal("2", "4");
        }

        [Fact]
        public void AddAnswer_TrueFalse_StoresTheValue()
        {
            var quiz = NewQuiz();

            quiz.AddAnswer(QuizType.TrueFalse, new[] { "x" }, new[] { "True", "False" }, trueFalseValue: true);

            quiz.Answer.Type.Should().Be(QuizType.TrueFalse);
            quiz.Answer.CorrectAnswers.Should().Equal("True");
        }

        [Fact]
        public void AddAnswer_WithoutAnswersOrOptions_Throws()
        {
            var quiz = NewQuiz();

            ((Action)(() => quiz.AddAnswer(QuizType.SingleChoice, null, new[] { "a" }))).Should().Throw<DomainException>();
            ((Action)(() => quiz.AddAnswer(QuizType.SingleChoice, new[] { "a" }, null))).Should().Throw<DomainException>();
            ((Action)(() => quiz.AddAnswer(QuizType.SingleChoice, Array.Empty<string>(), new[] { "a" }))).Should().Throw<DomainException>();
            ((Action)(() => quiz.AddAnswer(QuizType.SingleChoice, new[] { "a" }, Array.Empty<string>()))).Should().Throw<DomainException>();
        }

        [Fact]
        public void AddAnswer_UnknownType_Throws()
        {
            var quiz = NewQuiz();

            var act = () => quiz.AddAnswer((QuizType)99, new[] { "a" }, new[] { "a", "b" });

            act.Should().Throw<DomainException>().WithMessage("Unsupported quiz type");
        }
    }

    public class QuizAnswerTests
    {
        [Fact]
        public void SingleChoice_TrimsTheAnswer()
        {
            var answer = QuizAnswer.SingleChoice("  Paris ", new[] { "Paris", "Rome" });

            answer.CorrectAnswers.Should().Equal("Paris");
            answer.Options.Should().Equal("Paris", "Rome");
        }

        [Fact]
        public void MultipleChoice_DropsBlankAnswers()
        {
            var answer = QuizAnswer.MultipleChoice(new[] { "a", " ", "", "b " });

            answer.CorrectAnswers.Should().Equal("a", "b");
            answer.Options.Should().BeNull();
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void SingleChoice_WithABlankAnswer_Throws(string correct)
        {
            var act = () => QuizAnswer.SingleChoice(correct);

            act.Should().Throw<DomainException>().WithMessage("At least one correct answer is required");
        }

        [Fact]
        public void MultipleChoice_WithOnlyBlankAnswers_Throws()
        {
            var act = () => QuizAnswer.MultipleChoice(new[] { " ", "" });

            act.Should().Throw<DomainException>();
        }

        [Theory]
        [InlineData(true, "True")]
        [InlineData(false, "False")]
        public void TrueFalse_StoresTheValueAsText_AndHasNoOptions(bool value, string text)
        {
            var answer = QuizAnswer.TrueFalse(value);

            answer.Type.Should().Be(QuizType.TrueFalse);
            answer.CorrectAnswers.Should().Equal(text);
            answer.Options.Should().BeNull();
        }
    }

    public class MaterialTests
    {
        private static Material Create(string url) =>
            new(Guid.NewGuid(), "Slides", "Week 1", url, MaterialType.Slide, Guid.NewGuid());

        [Theory]
        [InlineData("https://cdn.example.com/slides.pdf")]
        [InlineData("materials/week1.pdf")]
        [InlineData("/materials/week1.pdf")]
        public void AcceptsStoragePathsAndHttpsLinks(string url)
        {
            Create(url).Url.Should().Be(url);
        }

        [Fact]
        public void TrimsTheUrl()
        {
            Create("  https://cdn.example.com/a.pdf  ").Url.Should().Be("https://cdn.example.com/a.pdf");
        }

        [Theory]
        [InlineData("javascript:alert(1)")]
        [InlineData("data:text/html,<script>alert(1)</script>")]
        [InlineData("http://insecure.example.com/a.pdf")]
        [InlineData("//evil.example.com/a.pdf")]
        [InlineData("../secret.pdf")]
        [InlineData("")]
        public void RefusesLinksThatAreNotSafeToRender(string url)
        {
            var act = () => Create(url);

            act.Should().Throw<DomainException>().WithMessage("Material URL must be a storage path or an https URL");
        }

        [Fact]
        public void KeepsItsDetails()
        {
            var lessonId = Guid.NewGuid();
            var material = new Material(Guid.NewGuid(), "Slides", "Week 1", "materials/a.pdf", MaterialType.Pdf, lessonId);

            material.Name.Should().Be("Slides");
            material.Description.Should().Be("Week 1");
            material.Type.Should().Be(MaterialType.Pdf);
            material.LessonID.Should().Be(lessonId);
        }
    }

    public class CourseReviewTests
    {
        [Fact]
        public void Constructor_RecordsTheReview()
        {
            var courseId = Guid.NewGuid();
            var studentId = Guid.NewGuid();

            var review = new CourseReview(courseId, studentId, 4.5f, "Great");

            review.Id.Should().NotBeEmpty();
            review.CourseID.Should().Be(courseId);
            review.StudentID.Should().Be(studentId);
            review.Rating.Should().Be(4.5f);
            review.Comment.Should().Be("Great");
            review.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            review.DeleteAt.Should().BeNull();
        }

        [Fact]
        public void UpdateReview_ChangesTheRatingAndTheComment()
        {
            var review = new CourseReview(Guid.NewGuid(), Guid.NewGuid(), 3f, "Okay");

            review.UpdateReview(5f, "Much better");

            review.Rating.Should().Be(5f);
            review.Comment.Should().Be("Much better");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void UpdateReview_WithABlankComment_KeepsTheOldComment(string? comment)
        {
            var review = new CourseReview(Guid.NewGuid(), Guid.NewGuid(), 3f, "Okay");

            review.UpdateReview(4f, comment);

            review.Rating.Should().Be(4f);
            review.Comment.Should().Be("Okay");
        }

        [Fact]
        public void DeleteReview_MarksItDeleted_WithoutRemovingIt()
        {
            var review = new CourseReview(Guid.NewGuid(), Guid.NewGuid(), 3f, null);

            review.DeleteReview();

            review.DeleteAt.Should().NotBeNull();
        }
    }

    public class PolicyRuleTests
    {
        [Fact]
        public void Constructor_CreatesAnActiveRule()
        {
            var policyId = Guid.NewGuid();

            var rule = new PolicyRule(Guid.NewGuid(), "NO-ADS", "No advertising in lessons", policyId);

            rule.Code.Should().Be("NO-ADS");
            rule.Description.Should().Be("No advertising in lessons");
            rule.PolicyID.Should().Be(policyId);
            rule.IsActive.Should().BeTrue();
        }

        [Fact]
        public void Constructor_RefusesEmptyIds()
        {
            ((Action)(() => new PolicyRule(Guid.Empty, "C", "D", Guid.NewGuid()))).Should().Throw<DomainException>().WithMessage("Policy rule ID is required");
            ((Action)(() => new PolicyRule(Guid.NewGuid(), "C", "D", Guid.Empty))).Should().Throw<DomainException>().WithMessage("Policy ID is required");
        }

        [Theory]
        [InlineData("", "D", "Policy rule code is required")]
        [InlineData("  ", "D", "Policy rule code is required")]
        [InlineData("C", "", "Policy rule description is required")]
        [InlineData("C", "  ", "Policy rule description is required")]
        public void Constructor_RefusesABlankCodeOrDescription(string code, string description, string message)
        {
            var act = () => new PolicyRule(Guid.NewGuid(), code, description, Guid.NewGuid());

            act.Should().Throw<DomainException>().WithMessage(message);
        }
    }
}
