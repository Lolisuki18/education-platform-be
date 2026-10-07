using Application.Exceptions;
using Application.Features.Academic.Commands.UpdateGrade;
using Application.Features.Academic.Commands.UpdateSubject;
using Application.Interface;
using Domain.AcademicManagement.Aggregate;
using Domain.Common.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace UnitTests.Application.Features.Academic.Commands
{
    public class UpdateGradeAndSubjectTests
    {
        private readonly Mock<IUnitOfWork> _unitOfWork = new();
        private readonly Mock<IGradeRepository> _grades = new();
        private readonly Mock<ISubjectRepository> _subjects = new();
        private readonly Mock<ICurrentUser> _currentUser = new();
        private readonly Guid _admin = Guid.NewGuid();

        public UpdateGradeAndSubjectTests()
        {
            _unitOfWork.Setup(u => u.GetRepository<IGradeRepository>()).Returns(_grades.Object);
            _unitOfWork.Setup(u => u.GetRepository<ISubjectRepository>()).Returns(_subjects.Object);
            _currentUser.Setup(c => c.Id).Returns(_admin);
        }

        private UpdateGradeCommandHandler GradeHandler() => new(_unitOfWork.Object, _currentUser.Object);
        private UpdateSubjectCommandHandler SubjectHandler() => new(_unitOfWork.Object, _currentUser.Object);

        // ------------------------------------------------------------------ grade

        private Grade ExistingGrade()
        {
            var grade = new Grade(Guid.NewGuid(), "Grade 10");
            _grades.Setup(r => r.GetByIdAsync(grade.GradeID, It.IsAny<CancellationToken>())).ReturnsAsync(grade);
            return grade;
        }

        [Fact]
        public async Task UpdateGrade_RenamesAndCommits_TrimmingTheName()
        {
            var grade = ExistingGrade();

            await GradeHandler().Handle(new UpdateGradeCommand { GradeID = grade.GradeID, Name = "  Grade 11  ", IsActive = true }, CancellationToken.None);

            grade.Name.Should().Be("Grade 11");
            _unitOfWork.Verify(u => u.CommitAsync(_admin.ToString()), Times.Once);
        }

        [Fact]
        public async Task UpdateGrade_ChecksTheNameAgainstTheOtherGradesOnly()
        {
            var grade = ExistingGrade();

            await GradeHandler().Handle(new UpdateGradeCommand { GradeID = grade.GradeID, Name = "Grade 10", IsActive = true }, CancellationToken.None);

            _grades.Verify(r => r.NameExistsAsync("Grade 10", grade.GradeID, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateGrade_WithATakenName_IsAConflict()
        {
            var grade = ExistingGrade();
            _grades.Setup(r => r.NameExistsAsync("Grade 12", grade.GradeID, It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var act = () => GradeHandler().Handle(new UpdateGradeCommand { GradeID = grade.GradeID, Name = "Grade 12", IsActive = true }, CancellationToken.None);

            await act.Should().ThrowAsync<ConflictException>();
            _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task UpdateGrade_Unknown_IsNotFound()
        {
            _grades.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Grade?)null);

            var act = () => GradeHandler().Handle(new UpdateGradeCommand { GradeID = Guid.NewGuid(), Name = "X", IsActive = true }, CancellationToken.None);

            await act.Should().ThrowAsync<NotFoundException>();
        }

        [Fact]
        public async Task UpdateGrade_WithoutSignIn_IsRefused()
        {
            _currentUser.Setup(c => c.Id).Returns((Guid?)null);

            var act = () => GradeHandler().Handle(new UpdateGradeCommand { GradeID = Guid.NewGuid(), Name = "X" }, CancellationToken.None);

            await act.Should().ThrowAsync<AuthenticateException>();
        }

        [Fact]
        public async Task UpdateGrade_DeactivatingAGradeInUse_IsAConflict()
        {
            var grade = ExistingGrade();
            _grades.Setup(r => r.IsInUse(grade.GradeID, It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var act = () => GradeHandler().Handle(new UpdateGradeCommand { GradeID = grade.GradeID, Name = "Grade 10", IsActive = false }, CancellationToken.None);

            await act.Should().ThrowAsync<ConflictException>();
            grade.IsActive.Should().BeTrue();
            _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task UpdateGrade_DeactivatingAnUnusedGrade_Works()
        {
            var grade = ExistingGrade();

            await GradeHandler().Handle(new UpdateGradeCommand { GradeID = grade.GradeID, Name = "Grade 10", IsActive = false }, CancellationToken.None);

            grade.IsActive.Should().BeFalse();
            _unitOfWork.Verify(u => u.CommitAsync(_admin.ToString()), Times.Once);
        }

        [Fact]
        public async Task UpdateGrade_ReactivatingDoesNotAskWhetherItIsInUse()
        {
            var grade = ExistingGrade();
            grade.Deactivate();

            await GradeHandler().Handle(new UpdateGradeCommand { GradeID = grade.GradeID, Name = "Grade 10", IsActive = true }, CancellationToken.None);

            grade.IsActive.Should().BeTrue();
            _grades.Verify(r => r.IsInUse(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        // ------------------------------------------------------------------ subject

        private Subject ExistingSubject()
        {
            var subject = new Subject(Guid.NewGuid(), "MATH", "Mathematics", Guid.Empty);
            _subjects.Setup(r => r.GetByIdAsync(subject.SubjectID, It.IsAny<CancellationToken>())).ReturnsAsync(subject);
            return subject;
        }

        [Fact]
        public async Task UpdateSubject_ChangesCodeAndName_Trimmed()
        {
            var subject = ExistingSubject();

            await SubjectHandler().Handle(
                new UpdateSubjectCommand { SubjectID = subject.SubjectID, Code = " PHYS ", Name = " Physics ", IsActive = true }, CancellationToken.None);

            subject.Code.Should().Be("PHYS");
            subject.Name.Should().Be("Physics");
            _unitOfWork.Verify(u => u.CommitAsync(_admin.ToString()), Times.Once);
        }

        [Fact]
        public async Task UpdateSubject_WithATakenCode_IsAConflict()
        {
            var subject = ExistingSubject();
            _subjects.Setup(r => r.CodeExistsAsync("ENG", subject.SubjectID, It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var act = () => SubjectHandler().Handle(
                new UpdateSubjectCommand { SubjectID = subject.SubjectID, Code = "ENG", Name = "English", IsActive = true }, CancellationToken.None);

            await act.Should().ThrowAsync<ConflictException>();
            _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task UpdateSubject_Unknown_IsNotFound()
        {
            _subjects.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Subject?)null);

            var act = () => SubjectHandler().Handle(
                new UpdateSubjectCommand { SubjectID = Guid.NewGuid(), Code = "X", Name = "X" }, CancellationToken.None);

            await act.Should().ThrowAsync<NotFoundException>();
        }

        [Fact]
        public async Task UpdateSubject_WithoutSignIn_IsRefused()
        {
            _currentUser.Setup(c => c.Id).Returns((Guid?)null);

            var act = () => SubjectHandler().Handle(new UpdateSubjectCommand { SubjectID = Guid.NewGuid(), Code = "X", Name = "X" }, CancellationToken.None);

            await act.Should().ThrowAsync<AuthenticateException>();
        }

        [Fact]
        public async Task UpdateSubject_DeactivatingASubjectInUse_IsAConflict()
        {
            var subject = ExistingSubject();
            _subjects.Setup(r => r.IsInUse(subject.SubjectID, It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var act = () => SubjectHandler().Handle(
                new UpdateSubjectCommand { SubjectID = subject.SubjectID, Code = "MATH", Name = "Mathematics", IsActive = false }, CancellationToken.None);

            await act.Should().ThrowAsync<ConflictException>();
            subject.IsActive.Should().BeTrue();
        }

        [Fact]
        public async Task UpdateSubject_DeactivatingAnUnusedSubject_Works()
        {
            var subject = ExistingSubject();

            await SubjectHandler().Handle(
                new UpdateSubjectCommand { SubjectID = subject.SubjectID, Code = "MATH", Name = "Mathematics", IsActive = false }, CancellationToken.None);

            subject.IsActive.Should().BeFalse();
        }
    }
}
