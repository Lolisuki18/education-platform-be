using API.ExceptionHandlers;
using Application.Features.Users.Queries;
using Application.Interface;
using Application.Results;
using AutoMapper;
using Domain.Common.Interfaces;
using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.Enum;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace UnitTests.Api
{
    /// <summary>A client that disconnects must stop the work it started, and must not show up as a server error.</summary>
    public class CancellationTests
    {
        [Fact]
        public async Task ACancelledRequest_IsNotReportedAsAServerError()
        {
            using var cts = new CancellationTokenSource();
            var context = new DefaultHttpContext { RequestAborted = cts.Token };
            await cts.CancelAsync();

            var handled = await new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance)
                .TryHandleAsync(context, new OperationCanceledException(cts.Token), CancellationToken.None);

            handled.Should().BeTrue();
            context.Response.StatusCode.Should().Be(499);
        }

        [Fact]
        public async Task ACancellationThatIsNotTheClients_StaysAServerError()
        {
            // e.g. a timeout inside the application: the client is still waiting for an answer
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            await new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance)
                .TryHandleAsync(context, new OperationCanceledException(), CancellationToken.None);

            context.Response.StatusCode.Should().Be(500);
        }

        [Fact]
        public async Task QueryHandlers_PassTheRequestTokenToTheRepository()
        {
            using var cts = new CancellationTokenSource();
            var userId = Guid.NewGuid();
            var user = new User(userId, "a@example.com", "Secret123", "0900000000", "A", null, Role.Student, DateTime.UtcNow);

            var users = new Mock<IUserRepository>();
            users.Setup(r => r.GetByIdAsync(userId, cts.Token)).ReturnsAsync(user);

            var unitOfWork = new Mock<IUnitOfWork>();
            unitOfWork.Setup(u => u.GetRepository<IUserRepository>()).Returns(users.Object);

            var currentUser = new Mock<ICurrentUser>();
            currentUser.Setup(c => c.Id).Returns(userId);

            var mapper = new Mock<IMapper>();
            mapper.Setup(m => m.Map<UserDTO>(user)).Returns(new UserDTO { UserID = userId });

            var handler = new GetUserDetailsHandler(unitOfWork.Object, mapper.Object, currentUser.Object);

            await handler.Handle(new GetUserDetailsQuery(), cts.Token);

            users.Verify(r => r.GetByIdAsync(userId, cts.Token), Times.Once);
        }
    }
}
