using Domain.DomainExceptions;
using Domain.OrderManagement.Aggregate;
using FluentAssertions;
using System;
using Xunit;

namespace UnitTests.DomainTests.OrderManagement
{
    public class PenaltyTests
    {
        [Fact]
        public void PenaltyConstructor_EmptyPenaltyId_ShouldThrowDomainException()
        {
            Action act = () => new Penalty(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), 50m, "Reason");
            act.Should().Throw<DomainException>().WithMessage("Penalty ID cannot be empty");
        }

        [Fact]
        public void PenaltyConstructor_EmptyTeacherId_ShouldThrowDomainException()
        {
            Action act = () => new Penalty(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), 50m, "Reason");
            act.Should().Throw<DomainException>().WithMessage("Teacher ID cannot be empty");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-50)]
        public void PenaltyConstructor_InvalidAmount_ShouldThrowDomainException(decimal amount)
        {
            Action act = () => new Penalty(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), amount, "Reason");
            act.Should().Throw<DomainException>().WithMessage("Penalty amount must be greater than zero");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void PenaltyConstructor_InvalidReason_ShouldThrowDomainException(string? reason)
        {
            Action act = () => new Penalty(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 50m, reason!);
            act.Should().Throw<DomainException>().WithMessage("Penalty reason is required");
        }

        [Fact]
        public void PenaltyConstructor_ValidArguments_ShouldCreateSuccessfully()
        {
            var penaltyId = Guid.NewGuid();
            var teacherId = Guid.NewGuid();
            var courseId = Guid.NewGuid();

            var penalty = new Penalty(penaltyId, teacherId, courseId, 50m, "  Copyright Violation  ");

            penalty.PenaltyID.Should().Be(penaltyId);
            penalty.TeacherID.Should().Be(teacherId);
            penalty.CourseID.Should().Be(courseId);
            penalty.PenaltyAmount.Should().Be(50m);
            penalty.Reason.Should().Be("Copyright Violation");
            penalty.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }
    }
}
