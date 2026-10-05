using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Exceptions;
using Application.Features.Academic.Commands.CreateSubject;
using Application.Features.Academic.Commands.UpdateSubject;
using Application.Features.Academic.Commands.DeactivateSubject;
using Application.Interface;
using Domain.AcademicManagement.Aggregate;
using Domain.Common.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace UnitTests.Application.Features.Academic.Commands
{
    public class SubjectCommandsTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<ISubjectRepository> _mockSubjectRepository;
        private readonly Mock<ICurrentUser> _mockCurrentUser;
        private readonly Guid _currentUserId;

        public SubjectCommandsTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockSubjectRepository = new Mock<ISubjectRepository>();
            _mockCurrentUser = new Mock<ICurrentUser>();
            _currentUserId = Guid.NewGuid();

            _mockCurrentUser.Setup(u => u.Id).Returns(_currentUserId);
            _mockUnitOfWork
                .Setup(u => u.GetRepository<ISubjectRepository>())
                .Returns(_mockSubjectRepository.Object);
        }

        [Fact]
        public async Task CreateSubject_CodeExists_ShouldThrowConflict()
        {
            // Arrange
            var existingSubjects = new List<Subject> { new Subject(Guid.NewGuid(), "MATH", "Mathematics", Guid.Empty) };
            _mockSubjectRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(existingSubjects);

            var command = new CreateSubjectCommand { Code = "MATH", Name = "Maths" };
            var handler = new CreateSubjectCommandHandler(_mockUnitOfWork.Object, _mockCurrentUser.Object);

            // Act
            Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<ConflictException>().WithMessage("Subject with code 'MATH' already exists.");
        }

        [Fact]
        public async Task CreateSubject_Valid_ShouldAddAndCommit()
        {
            // Arrange
            _mockSubjectRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Subject>());

            var command = new CreateSubjectCommand { Code = "ENG", Name = "English" };
            var handler = new CreateSubjectCommandHandler(_mockUnitOfWork.Object, _mockCurrentUser.Object);

            // Act
            var result = await handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().NotBeEmpty();
            _mockSubjectRepository.Verify(r => r.Add(It.Is<Subject>(s => s.Code == "ENG" && s.Name == "English")), Times.Once);
            _mockUnitOfWork.Verify(u => u.CommitAsync(_currentUserId.ToString()), Times.Once);
        }

        [Fact]
        public async Task DeactivateSubject_InUse_ShouldThrowConflict()
        {
            // Arrange
            var subject = new Subject(Guid.NewGuid(), "MATH", "Mathematics", Guid.Empty);
            _mockSubjectRepository.Setup(r => r.GetByIdAsync(subject.SubjectID)).ReturnsAsync(subject);
            _mockSubjectRepository.Setup(r => r.IsInUse(subject.SubjectID)).ReturnsAsync(true);

            var command = new DeactivateSubjectCommand { SubjectID = subject.SubjectID };
            var handler = new DeactivateSubjectCommandHandler(_mockUnitOfWork.Object, _mockCurrentUser.Object);

            // Act
            Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<ConflictException>().WithMessage("Cannot deactivate subject because it is currently in use by courses or lessons.");
        }

        [Fact]
        public async Task DeactivateSubject_NotInUse_ShouldDeactivateAndCommit()
        {
            // Arrange
            var subject = new Subject(Guid.NewGuid(), "MATH", "Mathematics", Guid.Empty);
            _mockSubjectRepository.Setup(r => r.GetByIdAsync(subject.SubjectID)).ReturnsAsync(subject);
            _mockSubjectRepository.Setup(r => r.IsInUse(subject.SubjectID)).ReturnsAsync(false);

            var command = new DeactivateSubjectCommand { SubjectID = subject.SubjectID };
            var handler = new DeactivateSubjectCommandHandler(_mockUnitOfWork.Object, _mockCurrentUser.Object);

            // Act
            await handler.Handle(command, CancellationToken.None);

            // Assert
            subject.IsActive.Should().BeFalse();
            _mockUnitOfWork.Verify(u => u.CommitAsync(_currentUserId.ToString()), Times.Once);
        }
    }
}
