using Domain.AcademicManagement.Aggregate;
using Domain.DomainExceptions;
using FluentAssertions;
using System;
using Xunit;

namespace UnitTests.DomainTests.AcademicManagement
{
    public class AcademicTests
    {
        // ======================= GRADE TESTS =======================
        [Fact]
        public void GradeConstructor_EmptyId_ShouldThrowDomainException()
        {
            Action act = () => new Grade(Guid.Empty, "Grade 10");
            act.Should().Throw<DomainException>().WithMessage("Grade ID is required");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void GradeConstructor_InvalidName_ShouldThrowDomainException(string? name)
        {
            Action act = () => new Grade(Guid.NewGuid(), name!);
            act.Should().Throw<DomainException>().WithMessage("Grade name is required");
        }

        [Fact]
        public void GradeConstructor_ValidArguments_ShouldCreateSuccessfully()
        {
            var gradeId = Guid.NewGuid();
            var grade = new Grade(gradeId, "  Grade 10  ");

            grade.GradeID.Should().Be(gradeId);
            grade.Name.Should().Be("Grade 10");
            grade.IsActive.Should().BeTrue();
        }

        // ======================= SUBJECT TESTS =======================
        [Fact]
        public void SubjectConstructor_EmptyId_ShouldThrowDomainException()
        {
            Action act = () => new Subject(Guid.Empty, "MATH", "Mathematics", Guid.NewGuid());
            act.Should().Throw<DomainException>().WithMessage("Subject ID is required");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void SubjectConstructor_InvalidCode_ShouldThrowDomainException(string? code)
        {
            Action act = () => new Subject(Guid.NewGuid(), code!, "Mathematics", Guid.NewGuid());
            act.Should().Throw<DomainException>().WithMessage("Subject code is required");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void SubjectConstructor_InvalidName_ShouldThrowDomainException(string? name)
        {
            Action act = () => new Subject(Guid.NewGuid(), "MATH", name!, Guid.NewGuid());
            act.Should().Throw<DomainException>().WithMessage("Subject name is required");
        }

        [Fact]
        public void SubjectConstructor_ValidArguments_ShouldCreateSuccessfully()
        {
            var subjectId = Guid.NewGuid();
            var gradeId = Guid.NewGuid();

            var subject = new Subject(subjectId, "  MATH10  ", "  Mathematics 10  ", gradeId);

            subject.SubjectID.Should().Be(subjectId);
            subject.Code.Should().Be("MATH10");
            subject.Name.Should().Be("Mathematics 10");
            subject.IsActive.Should().BeTrue();
            subject.DefaultLessons.Should().BeEmpty();
        }
    }
}
