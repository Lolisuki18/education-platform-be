using Application.Exceptions;
using Application.Features.StudentReview.Command;
using Application.Interface;
using Application.Results;
using AutoMapper;
using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Entity;
using Domain.Common.Interfaces;
using Domain.EnrollmentManagement.Aggregate;
using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.Enum;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace UnitTests.Application.Features.StudentReview
{
    public class CreateReviewCommandHandlerTests
    {
        private readonly Mock<IUnitOfWork> _unitOfWork = new();
        private readonly Mock<IEnrollmentRepository> _enrollments = new();
        private readonly Mock<ICourseReviewRepository> _reviews = new();
        private readonly Mock<ICourseRepository> _courses = new();
        private readonly Mock<IUserRepository> _users = new();
        private readonly Mock<IMapper> _mapper = new();
        private readonly Mock<ICurrentUser> _currentUser = new();

        private readonly Guid _studentId = Guid.NewGuid();
        private readonly Guid _courseId = Guid.NewGuid();
        private readonly User _student;
        private readonly Course _course;
        private readonly CreateReviewCommandHandler _handler;

        public CreateReviewCommandHandlerTests()
        {
            _unitOfWork.Setup(u => u.GetRepository<IEnrollmentRepository>()).Returns(_enrollments.Object);
            _unitOfWork.Setup(u => u.GetRepository<ICourseReviewRepository>()).Returns(_reviews.Object);
            _unitOfWork.Setup(u => u.GetRepository<ICourseRepository>()).Returns(_courses.Object);
            _unitOfWork.Setup(u => u.GetRepository<IUserRepository>()).Returns(_users.Object);

            _currentUser.Setup(c => c.Id).Returns(_studentId);
            _currentUser.Setup(c => c.Role).Returns("Student");

            _student = new User(_studentId, "student@example.com", "Secret123", "0901234567", "Student Name", null, Role.Student, DateTime.UtcNow, true);
            _course = new Course(_courseId, "Algebra", "About algebra", 100m, "thumb.png", null, "none", "outcomes", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null);

            _enrollments
                .Setup(e => e.IsStudentEnrolled(_studentId, _courseId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _courses.Setup(c => c.GetByIdAsync(_courseId, It.IsAny<CancellationToken>())).ReturnsAsync(_course);
            _users.Setup(u => u.GetByIdAsync(_studentId, It.IsAny<CancellationToken>())).ReturnsAsync(_student);
            _mapper.Setup(m => m.Map<CourseReviewDTO>(It.IsAny<CourseReview>())).Returns(new CourseReviewDTO());

            _handler = new CreateReviewCommandHandler(_unitOfWork.Object, _mapper.Object, _currentUser.Object);
        }

        private CreateReviewCommand Command(float rating = 4.5f, string? comment = "Great") =>
            new() { CourseId = _courseId, Rating = rating, Comment = comment };

        [Fact]
        public async Task AnEnrolledStudent_CanReview_AndTheResultNamesTheCourseAndTheReviewer()
        {
            var result = await _handler.Handle(Command(), CancellationToken.None);

            result.CourseName.Should().Be("Algebra");
            result.Reviewer.Should().Be("Student Name");
            _reviews.Verify(r => r.AddAsync(It.Is<CourseReview>(x =>
                x.CourseID == _courseId && x.StudentID == _studentId && x.Rating == 4.5f && x.Comment == "Great"), It.IsAny<CancellationToken>()), Times.Once);
            _unitOfWork.Verify(u => u.CommitAsync(_studentId.ToString()), Times.Once);
        }

        [Fact]
        public async Task WithoutSignIn_IsRefused()
        {
            _currentUser.Setup(c => c.Id).Returns((Guid?)null);

            var act = () => _handler.Handle(Command(), CancellationToken.None);

            await act.Should().ThrowAsync<AuthenticateException>();
        }

        [Theory]
        [InlineData("Teacher")]
        [InlineData("Admin")]
        public async Task OnlyStudentsCanReview(string role)
        {
            _currentUser.Setup(c => c.Role).Returns(role);

            var act = () => _handler.Handle(Command(), CancellationToken.None);

            await act.Should().ThrowAsync<AuthenticateException>();
            _reviews.Verify(r => r.AddAsync(It.IsAny<CourseReview>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ANotEnrolledStudent_IsForbidden()
        {
            _enrollments
                .Setup(e => e.IsStudentEnrolled(_studentId, _courseId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var act = () => _handler.Handle(Command(), CancellationToken.None);

            await act.Should().ThrowAsync<ForbiddenException>();
            _reviews.Verify(r => r.AddAsync(It.IsAny<CourseReview>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ASecondReviewOfTheSameCourse_IsAConflict()
        {
            _reviews.Setup(r => r.HasStudentReviewedCourseAsync(_courseId, _studentId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var act = () => _handler.Handle(Command(), CancellationToken.None);

            await act.Should().ThrowAsync<ConflictException>();
            _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task LosingTheRaceToTheUniqueIndex_IsAlsoAConflict()
        {
            _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<string?>())).ThrowsAsync(new DbUpdateException("duplicate"));

            var act = () => _handler.Handle(Command(), CancellationToken.None);

            await act.Should().ThrowAsync<ConflictException>();
        }

        [Fact]
        public async Task AMissingCourse_IsNotFound()
        {
            _courses.Setup(c => c.GetByIdAsync(_courseId, It.IsAny<CancellationToken>())).ReturnsAsync((Course?)null);

            var act = () => _handler.Handle(Command(), CancellationToken.None);

            await act.Should().ThrowAsync<NotFoundException>().WithMessage("Course not found.");
        }

        [Fact]
        public async Task AMissingStudentRecord_IsNotFound()
        {
            _users.Setup(u => u.GetByIdAsync(_studentId, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

            var act = () => _handler.Handle(Command(), CancellationToken.None);

            await act.Should().ThrowAsync<NotFoundException>().WithMessage("Student not found.");
        }
    }
}
