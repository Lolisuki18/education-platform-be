using Application.Exceptions;
using Application.Features.Users.Commands;
using Application.Interface;
using Domain.AuditManagement.Aggregate;
using Domain.Common.Interfaces;
using Domain.CourseManagement.Aggregate;
using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.Enum;
using Domain.NotificationManagement.Aggregate;
using Domain.OrderManagement.Aggregate;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace UnitTests.Application.Features.Users.Commands
{
    public class AccountErasureHandlerTests
    {
        private const string Password = "Secret123";

        private readonly Mock<IUnitOfWork> _unitOfWork = new();
        private readonly Mock<IUserRepository> _users = new();
        private readonly Mock<IOrderRepository> _orders = new();
        private readonly Mock<ICourseRepository> _courses = new();
        private readonly Mock<INotificationRepository> _notifications = new();
        private readonly Mock<IAuditRepository> _audit = new();
        private readonly Mock<ICurrentUser> _currentUser = new();
        private readonly Mock<IUserActivityCache> _cache = new();
        private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero));

        private readonly DeleteMyAccountCommandHandler _deleteMine;
        private readonly DeleteUserCommandHandler _deleteOther;

        public AccountErasureHandlerTests()
        {
            _unitOfWork.Setup(u => u.GetRepository<IUserRepository>()).Returns(_users.Object);
            _unitOfWork.Setup(u => u.GetRepository<IOrderRepository>()).Returns(_orders.Object);
            _unitOfWork.Setup(u => u.GetRepository<ICourseRepository>()).Returns(_courses.Object);
            _unitOfWork.Setup(u => u.GetRepository<INotificationRepository>()).Returns(_notifications.Object);
            _unitOfWork.Setup(u => u.GetRepository<IAuditRepository>()).Returns(_audit.Object);

            _deleteMine = new DeleteMyAccountCommandHandler(_unitOfWork.Object, _currentUser.Object, _cache.Object, _time);
            _deleteOther = new DeleteUserCommandHandler(_unitOfWork.Object, _currentUser.Object, _cache.Object, _time);
        }

        private static User CreateUser(Role role = Role.Student)
            => new(Guid.NewGuid(), $"{Guid.NewGuid():N}@example.com", Password, "0901234567", "Real Name", null, role, DateTime.UtcNow, isVerified: true);

        private User SignedInUser(Role role = Role.Student)
        {
            var user = CreateUser(role);
            _currentUser.Setup(c => c.Id).Returns(user.UserID);
            _users.Setup(r => r.GetByIdWithSessions(user.UserID, It.IsAny<CancellationToken>())).ReturnsAsync(user);
            return user;
        }

        [Fact]
        public async Task DeleteMyAccount_WithTheRightPassword_ErasesAndCleansUp()
        {
            var user = SignedInUser();

            await _deleteMine.Handle(new DeleteMyAccountCommand { Password = Password }, CancellationToken.None);

            user.IsDeleted.Should().BeTrue();
            _unitOfWork.Verify(u => u.CommitAsync(user.UserID.ToString()), Times.Once);
            _cache.Verify(c => c.Invalidate(user.UserID), Times.Once);
            _notifications.Verify(n => n.DeleteAllForUserAsync(user.UserID, It.IsAny<CancellationToken>()), Times.Once);
            _audit.Verify(a => a.DeleteUserEntriesAsync(user.UserID, _time.GetUtcNow().UtcDateTime, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteMyAccount_WithAWrongPassword_ChangesNothing()
        {
            var user = SignedInUser();

            var act = () => _deleteMine.Handle(new DeleteMyAccountCommand { Password = "Wrong-password1" }, CancellationToken.None);

            await act.Should().ThrowAsync<BadRequestException>();
            user.IsDeleted.Should().BeFalse();
            _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task DeleteMyAccount_WithoutSignIn_IsRefused()
        {
            _currentUser.Setup(c => c.Id).Returns((Guid?)null);

            var act = () => _deleteMine.Handle(new DeleteMyAccountCommand { Password = Password }, CancellationToken.None);

            await act.Should().ThrowAsync<AuthenticateException>();
        }

        [Fact]
        public async Task DeleteMyAccount_WhilePaymentIsInProgress_IsRefused()
        {
            var user = SignedInUser();
            _orders.Setup(o => o.HasOpenOrderAsync(user.UserID, It.IsAny<DateTime>())).ReturnsAsync(true);

            var act = () => _deleteMine.Handle(new DeleteMyAccountCommand { Password = Password }, CancellationToken.None);

            await act.Should().ThrowAsync<ConflictException>();
            user.IsDeleted.Should().BeFalse();
        }

        [Fact]
        public async Task DeleteMyAccount_TeacherWithPublishedCourses_IsRefused()
        {
            var user = SignedInUser(Role.Teacher);
            _courses.Setup(c => c.HasPublishedCourseAsync(user.UserID, It.IsAny<CancellationToken>())).ReturnsAsync(true);

            var act = () => _deleteMine.Handle(new DeleteMyAccountCommand { Password = Password }, CancellationToken.None);

            await act.Should().ThrowAsync<ConflictException>();
            user.IsDeleted.Should().BeFalse();
        }

        [Fact]
        public async Task DeleteMyAccount_StudentIsNotAskedAboutCourses()
        {
            var user = SignedInUser();

            await _deleteMine.Handle(new DeleteMyAccountCommand { Password = Password }, CancellationToken.None);

            _courses.Verify(c => c.HasPublishedCourseAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            user.IsDeleted.Should().BeTrue();
        }

        [Fact]
        public async Task DeleteMyAccount_LastAdministrator_IsRefused()
        {
            var user = SignedInUser(Role.Admin);
            _users.Setup(r => r.GetUserIdsByRoleAsync(Role.Admin, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Guid> { user.UserID });

            var act = () => _deleteMine.Handle(new DeleteMyAccountCommand { Password = Password }, CancellationToken.None);

            await act.Should().ThrowAsync<ConflictException>();
            user.IsDeleted.Should().BeFalse();
        }

        [Fact]
        public async Task DeleteMyAccount_AdministratorWithAnotherAdminLeft_IsAllowed()
        {
            var user = SignedInUser(Role.Admin);
            _users.Setup(r => r.GetUserIdsByRoleAsync(Role.Admin, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Guid> { user.UserID, Guid.NewGuid() });

            await _deleteMine.Handle(new DeleteMyAccountCommand { Password = Password }, CancellationToken.None);

            user.IsDeleted.Should().BeTrue();
        }

        [Fact]
        public async Task DeleteUser_AlreadyDeleted_IsRefused()
        {
            var target = CreateUser();
            target.Erase(DateTime.UtcNow);
            _currentUser.Setup(c => c.Id).Returns(Guid.NewGuid());
            _users.Setup(r => r.GetByIdWithSessions(target.UserID, It.IsAny<CancellationToken>())).ReturnsAsync(target);

            var act = () => _deleteOther.Handle(new DeleteUserCommand { UserId = target.UserID }, CancellationToken.None);

            await act.Should().ThrowAsync<BadRequestException>().WithMessage("*already deleted*");
            _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task DeleteUser_ByAnAdministrator_Erases()
        {
            var admin = Guid.NewGuid();
            var target = CreateUser();
            _currentUser.Setup(c => c.Id).Returns(admin);
            _users.Setup(r => r.GetByIdWithSessions(target.UserID, It.IsAny<CancellationToken>())).ReturnsAsync(target);

            await _deleteOther.Handle(new DeleteUserCommand { UserId = target.UserID }, CancellationToken.None);

            target.IsDeleted.Should().BeTrue();
            _unitOfWork.Verify(u => u.CommitAsync(admin.ToString()), Times.Once);
            _cache.Verify(c => c.Invalidate(target.UserID), Times.Once);
        }

        [Fact]
        public async Task DeleteUser_Yourself_IsRefused()
        {
            var user = SignedInUser();

            var act = () => _deleteOther.Handle(new DeleteUserCommand { UserId = user.UserID }, CancellationToken.None);

            await act.Should().ThrowAsync<BadRequestException>();
            user.IsDeleted.Should().BeFalse();
        }

        [Fact]
        public async Task DeleteUser_Unknown_IsNotFound()
        {
            _currentUser.Setup(c => c.Id).Returns(Guid.NewGuid());

            var act = () => _deleteOther.Handle(new DeleteUserCommand { UserId = Guid.NewGuid() }, CancellationToken.None);

            await act.Should().ThrowAsync<NotFoundException>();
        }

        [Fact]
        public void Validators_RequireTheirInput()
        {
            new DeleteMyAccountCommandValidator().Validate(new DeleteMyAccountCommand { Password = "" }).IsValid.Should().BeFalse();
            new DeleteMyAccountCommandValidator().Validate(new DeleteMyAccountCommand { Password = Password }).IsValid.Should().BeTrue();
            new DeleteUserCommandValidator().Validate(new DeleteUserCommand { UserId = Guid.Empty }).IsValid.Should().BeFalse();
        }
    }
}
