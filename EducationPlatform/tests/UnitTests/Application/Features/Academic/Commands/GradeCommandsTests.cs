using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Exceptions;
using Application.Features.Academic.Commands.CreateGrade;
using Application.Features.Academic.Commands.UpdateGrade;
using Application.Features.Academic.Commands.DeactivateGrade;
using Application.Interface;
using Domain.AcademicManagement.Aggregate;
using Domain.Common.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace UnitTests.Application.Features.Academic.Commands
{
    public class GradeCommandsTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IGradeRepository> _mockGradeRepository;
        private readonly Mock<ICurrentUser> _mockCurrentUser;
        private readonly Guid _currentUserId;

        public GradeCommandsTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockGradeRepository = new Mock<IGradeRepository>();
            _mockCurrentUser = new Mock<ICurrentUser>();
            _currentUserId = Guid.NewGuid();

            _mockCurrentUser.Setup(u => u.Id).Returns(_currentUserId);
            _mockUnitOfWork
                .Setup(u => u.GetRepository<IGradeRepository>())
                .Returns(_mockGradeRepository.Object);
        }

        [Fact]
        public async Task CreateGrade_NameExists_ShouldThrowConflict()
        {
            // Arrange
            var existingGrades = new List<Grade> { new Grade(Guid.NewGuid(), "Grade 10") };
            _mockGradeRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(existingGrades);

            var command = new CreateGradeCommand { Name = "Grade 10" };
            var handler = new CreateGradeCommandHandler(_mockUnitOfWork.Object, _mockCurrentUser.Object);

            // Act
            Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<ConflictException>().WithMessage("Grade with name 'Grade 10' already exists.");
        }

        [Fact]
        public async Task CreateGrade_Valid_ShouldAddAndCommit()
        {
            // Arrange
            _mockGradeRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Grade>());

            var command = new CreateGradeCommand { Name = "Grade 11" };
            var handler = new CreateGradeCommandHandler(_mockUnitOfWork.Object, _mockCurrentUser.Object);

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().NotBeEmpty();
            _mockGradeRepository.Verify(r => r.Add(It.Is<Grade>(g => g.Name == "Grade 11")), Times.Once);
            _mockUnitOfWork.Verify(u => u.CommitAsync(_currentUserId.ToString()), Times.Once);
        }

        [Fact]
        public async Task DeactivateGrade_InUse_ShouldThrowConflict()
        {
            // Arrange
            var grade = new Grade(Guid.NewGuid(), "Grade 10");
            _mockGradeRepository.Setup(r => r.GetByIdAsync(grade.GradeID)).ReturnsAsync(grade);
            _mockGradeRepository.Setup(r => r.IsInUse(grade.GradeID)).ReturnsAsync(true);

            var command = new DeactivateGradeCommand { GradeID = grade.GradeID };
            var handler = new DeactivateGradeCommandHandler(_mockUnitOfWork.Object, _mockCurrentUser.Object);

            // Act
            Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<ConflictException>().WithMessage("Cannot deactivate grade because it is currently in use by courses or lessons.");
        }

        [Fact]
        public async Task DeactivateGrade_NotInUse_ShouldDeactivateAndCommit()
        {
            // Arrange
            var grade = new Grade(Guid.NewGuid(), "Grade 10");
            _mockGradeRepository.Setup(r => r.GetByIdAsync(grade.GradeID)).ReturnsAsync(grade);
            _mockGradeRepository.Setup(r => r.IsInUse(grade.GradeID)).ReturnsAsync(false);

            var command = new DeactivateGradeCommand { GradeID = grade.GradeID };
            var handler = new DeactivateGradeCommandHandler(_mockUnitOfWork.Object, _mockCurrentUser.Object);

            // Act
            await handler.Handle(command, CancellationToken.None);

            // Assert
            grade.IsActive.Should().BeFalse();
            _mockUnitOfWork.Verify(u => u.CommitAsync(_currentUserId.ToString()), Times.Once);
        }
    }
}
