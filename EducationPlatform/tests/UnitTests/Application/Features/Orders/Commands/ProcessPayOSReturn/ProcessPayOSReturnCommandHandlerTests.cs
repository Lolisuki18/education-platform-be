using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Features.Orders.Commands.FinishOrder;
using Application.Features.Orders.Commands.ProcessPayOSReturn;
using Application.Interface;
using Application.Options;
using Application.Results;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace UnitTests.Application.Features.Orders.Commands.ProcessPayOSReturn
{
    public class ProcessPayOSReturnCommandHandlerTests
    {
        private const string ChecksumKey = "checksum-key";

        private readonly Mock<IPayOSSignatureVerifier> _verifier = new();
        private readonly Mock<ISender> _sender = new();
        private readonly ProcessPayOSReturnCommandHandler _handler;

        public ProcessPayOSReturnCommandHandlerTests()
        {
            _verifier
                .Setup(v => v.VerifyRedirectSignature(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), ChecksumKey))
                .Returns(true);

            _sender
                .Setup(s => s.Send(It.IsAny<FinishOrderCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OrderDTO());

            _handler = new ProcessPayOSReturnCommandHandler(
                _verifier.Object,
                _sender.Object,
                Options.Create(new PayOSOptions { ChecksumKey = ChecksumKey }),
                NullLogger<ProcessPayOSReturnCommandHandler>.Instance);
        }

        private static ProcessPayOSReturnCommand Paid() => new()
        {
            Status = "PAID",
            OrderCode = "123456",
            Id = "link-id",
            Code = "00",
            Cancel = "false",
            Signature = "sig"
        };

        [Fact]
        public async Task MissingSignature_ShouldBeRejectedWithoutTouchingTheOrder()
        {
            var command = Paid();
            command.Signature = null;

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsSignatureValid.Should().BeFalse();
            result.IsSuccess.Should().BeFalse();
            _sender.Verify(s => s.Send(It.IsAny<FinishOrderCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task WrongSignature_ShouldBeRejectedWithoutTouchingTheOrder()
        {
            _verifier
                .Setup(v => v.VerifyRedirectSignature(
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(false);

            var result = await _handler.Handle(Paid(), CancellationToken.None);

            result.IsSignatureValid.Should().BeFalse();
            _sender.Verify(s => s.Send(It.IsAny<FinishOrderCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task PaidRedirect_ShouldFinishTheOrder()
        {
            var result = await _handler.Handle(Paid(), CancellationToken.None);

            result.IsSignatureValid.Should().BeTrue();
            result.IsSuccess.Should().BeTrue();
            result.OrderCode.Should().Be(123456);
            _sender.Verify(s => s.Send(It.Is<FinishOrderCommand>(c => c.OrderCode == 123456), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData("CANCELLED", "true")]
        [InlineData("PAID", "true")]
        [InlineData("PENDING", "false")]
        public async Task CancelledOrUnpaidRedirect_ShouldNotFinishTheOrder(string status, string cancel)
        {
            var command = Paid();
            command.Status = status;
            command.Cancel = cancel;

            var result = await _handler.Handle(command, CancellationToken.None);

            result.IsSignatureValid.Should().BeTrue();
            result.IsSuccess.Should().BeFalse();
            _sender.Verify(s => s.Send(It.IsAny<FinishOrderCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task FailureWhileFinishing_ShouldNotBreakTheRedirect()
        {
            _sender
                .Setup(s => s.Send(It.IsAny<FinishOrderCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("database is down"));

            var result = await _handler.Handle(Paid(), CancellationToken.None);

            // The signed webhook retries later, so the student still lands on the success page
            result.IsSuccess.Should().BeTrue();
        }
    }
}
