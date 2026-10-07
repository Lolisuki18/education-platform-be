using API.ExceptionHandlers;
using Domain.Exceptions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace UnitTests.Api
{
    public class ExceptionMappingTests
    {
        private static async Task<(int Status, string Body)> Handle(Exception exception)
        {
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            await new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance)
                .TryHandleAsync(context, exception, CancellationToken.None);

            context.Response.Body.Position = 0;
            return (context.Response.StatusCode, await new StreamReader(context.Response.Body).ReadToEndAsync());
        }

        [Fact]
        public async Task ABrokenBusinessRule_IsABadRequest_WithItsMessage()
        {
            var (status, body) = await Handle(new DomainException("Invalid or expired code."));

            status.Should().Be(400);
            body.Should().Contain("Invalid or expired code.");
        }

        [Fact]
        public async Task AnUnexpectedException_IsA500_WithoutItsMessage()
        {
            var (status, body) = await Handle(new InvalidOperationException("secret connection string"));

            status.Should().Be(500);
            body.Should().NotContain("secret connection string");
        }
    }
}
