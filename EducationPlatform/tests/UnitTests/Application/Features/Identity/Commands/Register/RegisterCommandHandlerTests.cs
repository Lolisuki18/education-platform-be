using Application.Exceptions;
using Application.Features.Identity.Commands.Register;
using Application.Interface;
using Domain.Common.Interfaces;
using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.Enum;
using Domain.IdentityManagement.ValueObject;
using FluentAssertions;
using MediatR;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace UnitTests.Application.Features.Identity.Commands.Register
{
    public class RegisterCommandHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly RegisterCommandHandler _handler;

        public RegisterCommandHandlerTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockUserRepository = new Mock<IUserRepository>();

            _mockEmailService = new Mock<IEmailService>();

            _mockUnitOfWork
                .Setup(u => u.GetRepository<IUserRepository>())
                .Returns(_mockUserRepository.Object);

            _handler = new RegisterCommandHandler(_mockUnitOfWork.Object, _mockEmailService.Object);
        }

        [Fact]
        public async Task Handle_PhoneAlreadyExistsForDifferentUser_ShouldThrowConflictException()
        {
            // Arrange
            var email = "newuser@gmail.com";
            var phone = "0123456789";

            var userWithSamePhone = new User(
                Guid.NewGuid(),
                "other@gmail.com",
                "password123",
                phone,
                "Other User",
                null,
                Role.Student,
                DateTime.UtcNow,
                isVerified: true
            );

            _mockUserRepository
                .Setup(r => r.GetUserByEmail(email))
                .ReturnsAsync((User?)null);

            _mockUserRepository
                .Setup(r => r.GetUserByPhone(phone))
                .ReturnsAsync(userWithSamePhone);

            var command = new RegisterCommand
            {
                Email = email,
                Phone = phone,
                Password = "password123",
                Name = "New User",
                Role = (int)Role.Student
            };

            // Act
            Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<ConflictException>()
                .WithMessage($"User with phone {phone} already exists.");
        }

        [Fact]
        public async Task Handle_CannotRegisterAsAdmin_ShouldThrowConflictException()
        {
            // Arrange
            var command = new RegisterCommand
            {
                Email = "admin@gmail.com",
                Phone = "0123456789",
                Password = "password123",
                Name = "Admin User",
                Role = (int)Role.Admin
            };

            _mockUserRepository
                .Setup(r => r.GetUserByEmail(command.Email))
                .ReturnsAsync((User?)null);

            _mockUserRepository
                .Setup(r => r.GetUserByPhone(command.Phone))
                .ReturnsAsync((User?)null);

            // Act
            Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<ConflictException>()
                .WithMessage("Cannot register as Admin.");
        }

        [Fact]
        public async Task Handle_EmailAlreadyExistsAndVerified_ShouldThrowConflictException()
        {
            // Arrange
            var email = "verified@gmail.com";
            var phone = "0123456789";

            var verifiedUser = new User(
                Guid.NewGuid(),
                email,
                "password123",
                phone,
                "Verified User",
                null,
                Role.Student,
                DateTime.UtcNow,
                isVerified: true
            );

            _mockUserRepository
                .Setup(r => r.GetUserByEmail(email))
                .ReturnsAsync(verifiedUser);

            _mockUserRepository
                .Setup(r => r.GetUserByPhone(phone))
                .ReturnsAsync((User?)null);

            var command = new RegisterCommand
            {
                Email = email,
                Phone = phone,
                Password = "password123",
                Name = "Verified User",
                Role = (int)Role.Student
            };

            // Act
            Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<ConflictException>()
                .WithMessage($"User with email {email} already exists.");
        }

        [Fact]
        public async Task Handle_EmailAlreadyExistsButNotVerified_ShouldReuseUserAndRegenerateOtp()
        {
            // Arrange
            var email = "unverified@gmail.com";
            var phone = "0123456789";

            var unverifiedUser = new User(
                Guid.NewGuid(),
                email,
                "password123",
                phone,
                "Unverified User",
                null,
                Role.Student,
                DateTime.UtcNow,
                isVerified: false
            );

            _mockUserRepository
                .Setup(r => r.GetUserByEmail(email))
                .ReturnsAsync(unverifiedUser);

            _mockUserRepository
                .Setup(r => r.GetUserByPhone(phone))
                .ReturnsAsync((User?)null);

            var command = new RegisterCommand
            {
                Email = email,
                Phone = phone,
                Password = "password123",
                Name = "Unverified User",
                Role = (int)Role.Student
            };

            var originalOtp = unverifiedUser.EmailOtp;

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().Be(Unit.Value);

            // OTP should have been regenerated
            unverifiedUser.EmailOtp.Should().NotBeNullOrEmpty();
            unverifiedUser.EmailOtp.Should().NotBe(originalOtp);

            // The new details win: the earlier registration may have been made by someone else
            unverifiedUser.Name.Should().Be("Unverified User");
            unverifiedUser.Password.Verify("password123").Should().BeTrue();

            _mockUserRepository.Verify(r => r.Add(It.IsAny<User>()), Times.Never); // should NOT add new user
            _mockUnitOfWork.Verify(u => u.BeginTransactionAsync(), Times.Once);
            _mockUnitOfWork.Verify(u => u.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task Handle_NewUser_ShouldCreateUserAndCommit()
        {
            // Arrange
            var email = "newuser@gmail.com";
            var phone = "0987654321";

            _mockUserRepository
                .Setup(r => r.GetUserByEmail(email))
                .ReturnsAsync((User?)null);

            _mockUserRepository
                .Setup(r => r.GetUserByPhone(phone))
                .ReturnsAsync((User?)null);

            var command = new RegisterCommand
            {
                Email = email,
                Phone = phone,
                Password = "password123",
                Name = "New User",
                Role = (int)Role.Student
            };

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().Be(Unit.Value);

            _mockUserRepository.Verify(r => r.Add(It.Is<User>(u =>
                u.Email == email &&
                u.Phone == phone &&
                u.Name == "New User" &&
                u.Role == Role.Student &&
                !u.IsVerified &&
                u.EmailOtp != null
            )), Times.Once);

            _mockUnitOfWork.Verify(u => u.BeginTransactionAsync(), Times.Once);
            _mockUnitOfWork.Verify(u => u.CommitAsync(), Times.Once);
        }

        [Fact]
        public async Task Handle_ReRegisteringAnUnverifiedEmail_ShouldReplacePasswordSoTheFirstRegistrantCannotKeepIt()
        {
            var email = "victim@gmail.com";
            var attacker = new User(Guid.NewGuid(), email, "attacker-pass1", "0123456789", "Attacker", null, Role.Student, DateTime.UtcNow);
            _mockUserRepository.Setup(r => r.GetUserByEmail(email)).ReturnsAsync(attacker);
            _mockUserRepository.Setup(r => r.GetUserByPhone("0987654321")).ReturnsAsync((User?)null);

            await _handler.Handle(new RegisterCommand
            {
                Email = email,
                Phone = "0987654321",
                Password = "owner-pass1",
                Name = "Real Owner",
                Role = (int)Role.Student
            }, CancellationToken.None);

            attacker.Password.Verify("owner-pass1").Should().BeTrue();
            attacker.Password.Verify("attacker-pass1").Should().BeFalse();
            attacker.Name.Should().Be("Real Owner");
            attacker.Phone.Should().Be("0987654321");
        }

        [Fact]
        public async Task Handle_AskingForAnotherCodeTooSoon_ShouldBeThrottled()
        {
            var email = "pending@gmail.com";
            var pending = new User(Guid.NewGuid(), email, "password123", "0123456789", "Pending", null, Role.Student, DateTime.UtcNow);
            pending.GenerateEmailOtp(TimeSpan.FromMinutes(5)); // a code was just sent
            _mockUserRepository.Setup(r => r.GetUserByEmail(email)).ReturnsAsync(pending);
            _mockUserRepository.Setup(r => r.GetUserByPhone(It.IsAny<string>())).ReturnsAsync((User?)null);

            Func<Task> act = async () => await _handler.Handle(new RegisterCommand
            {
                Email = email,
                Phone = "0123456789",
                Password = "password123",
                Name = "Pending",
                Role = (int)Role.Student
            }, CancellationToken.None);

            await act.Should().ThrowAsync<global::Application.Exceptions.TooManyRequestsException>();
            _mockUnitOfWork.Verify(u => u.CommitAsync(It.IsAny<string?>()), Times.Never);
        }

        [Fact]
        public async Task Handle_NewUser_ShouldMailThePlainCodeThatMatchesTheStoredHash()
        {
            string? mailedOtp = null;
            _mockEmailService
                .Setup(e => e.SendVerificationEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((_, otp) => mailedOtp = otp)
                .Returns(Task.CompletedTask);

            User? created = null;
            _mockUserRepository.Setup(r => r.GetUserByEmail(It.IsAny<string>())).ReturnsAsync((User?)null);
            _mockUserRepository.Setup(r => r.GetUserByPhone(It.IsAny<string>())).ReturnsAsync((User?)null);
            _mockUserRepository.Setup(r => r.Add(It.IsAny<User>())).Callback<User>(u => created = u);

            await _handler.Handle(new RegisterCommand
            {
                Email = "new@gmail.com",
                Phone = "0987654321",
                Password = "password123",
                Name = "New",
                Role = (int)Role.Student
            }, CancellationToken.None);

            mailedOtp.Should().NotBeNull().And.HaveLength(6);
            created!.EmailOtp.Should().NotBe(mailedOtp);
            created.VerifyEmail(mailedOtp!); // does not throw: the mailed code is the right one
            created.IsVerified.Should().BeTrue();
        }
    }
}
