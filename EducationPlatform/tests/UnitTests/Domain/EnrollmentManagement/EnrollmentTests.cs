using Domain.Exceptions;
using Domain.EnrollmentManagement.Aggregate;
using Domain.EnrollmentManagement.Entity;
using Domain.EnrollmentManagement.Enum;
using FluentAssertions;
using System;
using System.Linq;
using Xunit;

namespace UnitTests.DomainTests.EnrollmentManagement
{
    public class EnrollmentTests
    {
        // ======================= ENROLLMENT CONSTRUCTOR TESTS =======================
        [Fact]
        public void EnrollmentConstructor_EmptyEnrollmentId_ShouldThrowDomainException()
        {
            Action act = () => new Enrollment(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
            act.Should().Throw<DomainException>().WithMessage("Enrollment ID cannot be empty");
        }

        [Fact]
        public void EnrollmentConstructor_EmptyStudentId_ShouldThrowDomainException()
        {
            Action act = () => new Enrollment(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), DateTime.UtcNow);
            act.Should().Throw<DomainException>().WithMessage("Student ID cannot be empty");
        }

        [Fact]
        public void EnrollmentConstructor_EmptyCourseId_ShouldThrowDomainException()
        {
            Action act = () => new Enrollment(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, DateTime.UtcNow);
            act.Should().Throw<DomainException>().WithMessage("Course ID cannot be empty");
        }

        [Fact]
        public void EnrollmentConstructor_ValidArguments_ShouldCreateSuccessfully()
        {
            var enrollmentId = Guid.NewGuid();
            var studentId = Guid.NewGuid();
            var courseId = Guid.NewGuid();
            var enrolledAt = DateTime.UtcNow;

            var enrollment = new Enrollment(enrollmentId, studentId, courseId, enrolledAt);

            enrollment.EnrollmentID.Should().Be(enrollmentId);
            enrollment.StudentID.Should().Be(studentId);
            enrollment.CourseID.Should().Be(courseId);
            enrollment.Status.Should().Be(EnrollmentStatus.Active);
            enrollment.EnrolledAt.Should().Be(enrolledAt);
            enrollment.CompletedAt.Should().BeNull();
            enrollment.CourseProgress.Should().NotBeNull();
            enrollment.CourseProgress.EnrollmentID.Should().Be(enrollmentId);
        }

        [Fact]
        public void Enrollment_CompleteEnrollment_ShouldSetCompletedAt()
        {
            var enrollment = new Enrollment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
            var completedAt = DateTime.UtcNow;

            enrollment.CompleteEnrollment(completedAt);

            enrollment.CompletedAt.Should().Be(completedAt);
            enrollment.Status.Should().Be(EnrollmentStatus.Completed);
        }

        // ======================= COURSE PROGRESS LOGIC TESTS =======================
        [Fact]
        public void CourseProgressConstructor_EmptyIds_ShouldThrowDomainException()
        {
            Action act1 = () => new CourseProgress(Guid.Empty, Guid.NewGuid());
            act1.Should().Throw<DomainException>().WithMessage("Course progress ID cannot be empty");

            Action act2 = () => new CourseProgress(Guid.NewGuid(), Guid.Empty);
            act2.Should().Throw<DomainException>().WithMessage("Enrollment ID cannot be empty");
        }

        [Fact]
        public void CourseProgress_AddChapterProgress_ShouldAddAndReturnChapterProgress()
        {
            var enrollmentId = Guid.NewGuid();
            var progress = new CourseProgress(Guid.NewGuid(), enrollmentId);
            var chapterId = Guid.NewGuid();

            var chapterProgress = progress.AddChapterProgress(chapterId);

            chapterProgress.Should().NotBeNull();
            chapterProgress.ChapterID.Should().Be(chapterId);
            chapterProgress.CourseProgressID.Should().Be(progress.CourseProgressID);
            progress.ChapterProgresses.Should().Contain(chapterProgress);
        }

        [Fact]
        public void ChapterProgressConstructor_EmptyIds_ShouldThrowDomainException()
        {
            Action act1 = () => new ChapterProgress(Guid.Empty, Guid.NewGuid(), Guid.NewGuid());
            act1.Should().Throw<DomainException>().WithMessage("Chapter progress ID cannot be empty");

            Action act2 = () => new ChapterProgress(Guid.NewGuid(), Guid.Empty, Guid.NewGuid());
            act2.Should().Throw<DomainException>().WithMessage("Course progress ID cannot be empty");

            Action act3 = () => new ChapterProgress(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty);
            act3.Should().Throw<DomainException>().WithMessage("Chapter ID cannot be empty");
        }

        [Fact]
        public void ChapterProgress_AddLessonProgress_ShouldAddAndReturnLessonProgress()
        {
            var chapterProgress = new ChapterProgress(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            var lessonId = Guid.NewGuid();

            var lessonProgress = chapterProgress.AddLessonProgress(lessonId);

            lessonProgress.Should().NotBeNull();
            lessonProgress.LessonID.Should().Be(lessonId);
            lessonProgress.ChapterProgressID.Should().Be(chapterProgress.ChapterProgressID);
            chapterProgress.LessonProgresses.Should().Contain(lessonProgress);
        }

        [Fact]
        public void LessonProgressConstructor_EmptyIds_ShouldThrowDomainException()
        {
            Action act1 = () => new LessonProgress(Guid.Empty, Guid.NewGuid(), Guid.NewGuid());
            act1.Should().Throw<DomainException>().WithMessage("Lesson progress ID cannot be empty");

            Action act2 = () => new LessonProgress(Guid.NewGuid(), Guid.Empty, Guid.NewGuid());
            act2.Should().Throw<DomainException>().WithMessage("Course progress ID cannot be empty"); // actual message in codebase is "Course progress ID cannot be empty"

            Action act3 = () => new LessonProgress(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty);
            act3.Should().Throw<DomainException>().WithMessage("Lesson ID cannot be empty");
        }

        [Fact]
        public void LessonProgress_MarkCompleted_ShouldSetIsCompletedAndCompletedAt()
        {
            var lessonProgress = new LessonProgress(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

            lessonProgress.MarkCompleted();

            lessonProgress.IsCompleted.Should().BeTrue();
            lessonProgress.CompletedAt.Should().NotBeNull();
            lessonProgress.CompletedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void LessonProgress_RecalculateCompletion_WithoutQuizzes_DoesNotCompleteTheLesson()
        {
            // A lesson without quizzes is finished when the student says so, never by merely being opened
            var lessonProgress = new LessonProgress(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

            lessonProgress.RecalculateCompletion(totalQuizzes: 0);

            lessonProgress.IsCompleted.Should().BeFalse();
        }

        [Fact]
        public void LessonProgress_RecalculateCompletion_NeedsEveryQuizOfTheLessonToBeCorrect()
        {
            var lessonProgress = new LessonProgress(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            var quiz1 = lessonProgress.AddQuizProgress(Guid.NewGuid());
            var quiz2 = lessonProgress.AddQuizProgress(Guid.NewGuid());

            // Case 1: nothing correct yet
            quiz1.RegisterAttempt(false);
            lessonProgress.RecalculateCompletion(totalQuizzes: 3);
            lessonProgress.IsCompleted.Should().BeFalse();

            // Case 2: both attempted quizzes are correct, but the lesson has a third one nobody tried
            quiz1.RegisterAttempt(true);
            quiz2.RegisterAttempt(true);
            lessonProgress.RecalculateCompletion(totalQuizzes: 3);
            lessonProgress.IsCompleted.Should().BeFalse();

            // Case 3: the lesson only has those two quizzes
            lessonProgress.RecalculateCompletion(totalQuizzes: 2);
            lessonProgress.IsCompleted.Should().BeTrue();
            lessonProgress.CompletedAt.Should().NotBeNull();
        }

        [Fact]
        public void QuizProgressConstructor_EmptyIds_ShouldThrowDomainException()
        {
            Action act1 = () => new QuizProgress(Guid.Empty, Guid.NewGuid(), Guid.NewGuid());
            act1.Should().Throw<DomainException>().WithMessage("Quiz Progress ID cannot be empty");

            Action act2 = () => new QuizProgress(Guid.NewGuid(), Guid.Empty, Guid.NewGuid());
            act2.Should().Throw<DomainException>().WithMessage("Lesson progress ID cannot be empty");

            Action act3 = () => new QuizProgress(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty);
            act3.Should().Throw<DomainException>().WithMessage("Quiz ID cannot be empty");
        }

        [Fact]
        public void QuizProgress_RegisterAttempt_ShouldIncrementAttemptCountAndSetIsCorrect()
        {
            var quizProgress = new QuizProgress(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

            // First attempt: incorrect
            quizProgress.RegisterAttempt(false);
            quizProgress.AttemptCount.Should().Be(1);
            quizProgress.IsCorrect.Should().BeFalse();
            quizProgress.LastAttemptAt.Should().NotBeNull();

            // Second attempt: correct
            quizProgress.RegisterAttempt(true);
            quizProgress.AttemptCount.Should().Be(2);
            quizProgress.IsCorrect.Should().BeTrue();
        }

        [Fact]
        public void CourseProgress_RecalculateCompletion_ShouldCalculateCompletionRateAndMarkCompletedCorrectly()
        {
            var enrollment = new Enrollment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
            var progress = enrollment.CourseProgress;

            // The course: 2 chapters, 3 lessons
            var chapterId1 = Guid.NewGuid();
            var chapterId2 = Guid.NewGuid();
            var lessonId1_1 = Guid.NewGuid();
            var lessonId1_2 = Guid.NewGuid();
            var lessonId2_1 = Guid.NewGuid();
            var outline = new[]
            {
                new LessonOutline(lessonId1_1, chapterId1, 0),
                new LessonOutline(lessonId1_2, chapterId1, 0),
                new LessonOutline(lessonId2_1, chapterId2, 0)
            };

            var chapter1 = progress.AddChapterProgress(chapterId1);
            var chapter2 = progress.AddChapterProgress(chapterId2);
            var lesson1_1 = chapter1.AddLessonProgress(lessonId1_1);
            var lesson1_2 = chapter1.AddLessonProgress(lessonId1_2);
            var lesson2_1 = chapter2.AddLessonProgress(lessonId2_1);

            // Initial: 0 completed lessons -> Rate = 0%
            progress.RecalculateCompletion(outline);
            progress.CompletionRate.Should().Be(0m);
            progress.IsCompleted.Should().BeFalse();

            // Complete 1 lesson -> Rate = 1/3 * 100 = 33.33%
            lesson1_1.MarkCompleted();
            progress.RecalculateCompletion(outline);
            progress.CompletionRate.Should().Be(33.33m);
            progress.IsCompleted.Should().BeFalse();
            chapter1.IsCompleted.Should().BeFalse(); // Not all lessons in chapter 1 are completed

            // Complete second lesson in chapter 1 -> Chapter 1 should be completed, Rate = 2/3 * 100 = 66.67%
            lesson1_2.MarkCompleted();
            progress.RecalculateCompletion(outline);
            progress.CompletionRate.Should().Be(66.67m);
            progress.IsCompleted.Should().BeFalse();
            chapter1.IsCompleted.Should().BeTrue();

            // Complete last lesson in chapter 2 -> Course progress should be fully completed, Rate = 100%
            lesson2_1.MarkCompleted();
            progress.RecalculateCompletion(outline);
            progress.CompletionRate.Should().Be(100m);
            progress.IsCompleted.Should().BeTrue();
            chapter2.IsCompleted.Should().BeTrue();
        }

        [Fact]
        public void CourseProgress_RecalculateCompletion_CountsLessonsTheStudentHasNotTouched()
        {
            var enrollment = new Enrollment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
            var progress = enrollment.CourseProgress;

            var chapterId = Guid.NewGuid();
            var firstLesson = Guid.NewGuid();
            var outline = new[]
            {
                new LessonOutline(firstLesson, chapterId, 0),
                new LessonOutline(Guid.NewGuid(), chapterId, 0),
                new LessonOutline(Guid.NewGuid(), chapterId, 0),
                new LessonOutline(Guid.NewGuid(), chapterId, 0)
            };

            // Only the first lesson has any progress record at all
            var chapter = progress.AddChapterProgress(chapterId);
            chapter.AddLessonProgress(firstLesson).MarkCompleted();

            progress.RecalculateCompletion(outline);

            progress.CompletionRate.Should().Be(25m);
            progress.IsCompleted.Should().BeFalse();
            chapter.IsCompleted.Should().BeFalse();
        }

        [Fact]
        public void CourseProgress_RecalculateCompletion_OfACourseWithoutLessons_IsNeverComplete()
        {
            var enrollment = new Enrollment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

            enrollment.CourseProgress.RecalculateCompletion(Array.Empty<LessonOutline>());

            enrollment.CourseProgress.CompletionRate.Should().Be(0m);
            enrollment.CourseProgress.IsCompleted.Should().BeFalse();
        }
    }
}
