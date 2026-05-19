using Xunit;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Features.Identity.Commands.Login;
using Domain.Common.Interfaces;
using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.ValueObject;
using Application.BusinessException;

public class LoginCommandTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly LoginCommandHandler _handler;

    public LoginCommandTests()
    {
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _userRepoMock = new Mock<IUserRepository>();

        _unitOfWorkMock
            .Setup(u => u.GetRepository<IUserRepository>())
            .Returns(_userRepoMock.Object);

        _handler = new LoginCommandHandler(_unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Login_Success_ShouldReturnToken()
    {
        // Arrange
        var email = "test@gmail.com";
        var password = "123456";

        var user = new User(
            Guid.NewGuid(),
            email,
            password,
            "0123456789",
            "Test User",
            null,
            Role.Student,
            null,
            true // verified
        );

        _userRepoMock
            .Setup(r => r.GetUserByEmail(email))
            .ReturnsAsync(user);

        var command = new LoginCommand
        {
            Email = email,
            Password = password
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result.Token));
        Assert.False(string.IsNullOrEmpty(result.RefreshToken));

        _unitOfWorkMock.Verify(u => u.BeginTransactionAsync(), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(), Times.Once);
    }

    [Fact]
    public async Task Login_UserNotFound_ShouldThrowNotFound()
    {
        // Arrange
        _userRepoMock
            .Setup(r => r.GetUserByEmail(It.IsAny<string>()))
            .ReturnsAsync((User?)null);

        var command = new LoginCommand
        {
            Email = "notfound@gmail.com",
            Password = "123456"
        };

        // Act & Assert
        await Assert.ThrowsAsync<NotFound>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Login_InvalidPassword_ShouldThrowAuthenticateException()
    {
        // Arrange
        var user = new User(
            Guid.NewGuid(),
            "test@gmail.com",
            "correct-password",
            "0123456789",
            "Test User",
            null,
            Role.Student,
            null,
            false // ❗ chưa verify
        );

        _userRepoMock
            .Setup(r => r.GetUserByEmail(It.IsAny<string>()))
            .ReturnsAsync(user);

        var command = new LoginCommand
        {
            Email = "test@gmail.com",
            Password = "wrong-password"
        };

        // Act & Assert
        await Assert.ThrowsAsync<AuthenticateException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }
}