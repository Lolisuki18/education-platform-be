using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Application.Exceptions;
using Application.Features.Orders.Commands.FinishOrder;
using Application.Features.Orders.Commands.ProcessPayOSWebhook;
using Application.Interface;
using Application.Options;
using Application.Results;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace UnitTests.Application.Features.Orders.Commands.ProcessPayOSWebhook
{
    public class ProcessPayOSWebhookCommandHandlerTests
    {
        private const string ChecksumKey = "checksum-key";

        private readonly Mock<IPayOSSignatureVerifier> _verifier = new();
        private readonly Mock<ISender> _sender = new();
        private readonly ProcessPayOSWebhookCommandHandler _handler;

        public ProcessPayOSWebhookCommandHandlerTests()
        {
            _verifier
                .Setup(v => v.VerifyWebhookSignature(It.IsAny<IDictionary<string, string?>>(), It.IsAny<string>(), ChecksumKey))
                .Returns(true);

            _sender
                .Setup(s => s.Send(It.IsAny<FinishOrderCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OrderDTO());

            _handler = new ProcessPayOSWebhookCommandHandler(
                _verifier.Object,
                _sender.Object,
                Options.Create(new PayOSOptions { ChecksumKey = ChecksumKey }),
                NullLogger<ProcessPayOSWebhookCommandHandler>.Instance);
        }

        private Task<PayOSWebhookResult> Handle(string body) =>
            _handler.Handle(new ProcessPayOSWebhookCommand { Body = body }, CancellationToken.None);

        private const string PaidBody =
            "{\"code\":\"00\",\"desc\":\"success\",\"data\":{\"orderCode\":1700000000000123,\"amount\":50000,\"paid\":true,\"note\":null},\"signature\":\"sig\"}";

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task EmptyBody_ShouldBeIgnored(string body)
        {
            (await Handle(body)).Should().Be(PayOSWebhookResult.Ignored);
            _sender.Verify(s => s.Send(It.IsAny<FinishOrderCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData("not json")]
        [InlineData("[]")]
        [InlineData("{\"code\":\"00\"}")]
        [InlineData("{\"code\":\"00\",\"data\":{},\"signature\":5}")]
        public async Task MalformedBody_ShouldBeRejected(string body)
        {
            (await Handle(body)).Should().Be(PayOSWebhookResult.Malformed);
        }

        [Fact]
        public async Task InvalidSignature_ShouldNotFinishAnything()
        {
            _verifier
                .Setup(v => v.VerifyWebhookSignature(It.IsAny<IDictionary<string, string?>>(), It.IsAny<string>(), ChecksumKey))
                .Returns(false);

            (await Handle(PaidBody)).Should().Be(PayOSWebhookResult.InvalidSignature);
            _sender.Verify(s => s.Send(It.IsAny<FinishOrderCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task FailedPayment_ShouldBeIgnored()
        {
            var body = PaidBody.Replace("\"code\":\"00\"", "\"code\":\"01\"");

            (await Handle(body)).Should().Be(PayOSWebhookResult.Ignored);
            _sender.Verify(s => s.Send(It.IsAny<FinishOrderCommand>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task PaidWebhook_ShouldFinishTheOrderWithTheReportedAmount()
        {
            (await Handle(PaidBody)).Should().Be(PayOSWebhookResult.Processed);

            _sender.Verify(s => s.Send(
                It.Is<FinishOrderCommand>(c => c.OrderCode == 1700000000000123 && c.PaidAmount == 50000),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Signature_ShouldBeCheckedAgainstValuesRenderedTheWayPayOSSignedThem()
        {
            IDictionary<string, string?>? captured = null;
            _verifier
                .Setup(v => v.VerifyWebhookSignature(It.IsAny<IDictionary<string, string?>>(), "sig", ChecksumKey))
                .Callback<IDictionary<string, string?>, string, string>((data, _, _) => captured = data)
                .Returns(true);

            await Handle(PaidBody);

            captured.Should().NotBeNull();
            captured!["orderCode"].Should().Be("1700000000000123");
            captured["amount"].Should().Be("50000");
            captured["paid"].Should().Be("true");
            captured["note"].Should().Be(string.Empty);
        }

        [Fact]
        public async Task UnknownOrder_ShouldBeAcknowledgedWithoutRetry()
        {
            _sender
                .Setup(s => s.Send(It.IsAny<FinishOrderCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new NotFoundException("Order not found"));

            (await Handle(PaidBody)).Should().Be(PayOSWebhookResult.Ignored);
        }

        [Fact]
        public async Task AmountMismatch_ShouldBeAcknowledgedButNotApplied()
        {
            _sender
                .Setup(s => s.Send(It.IsAny<FinishOrderCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new BadRequestException("Paid amount does not match"));

            (await Handle(PaidBody)).Should().Be(PayOSWebhookResult.Ignored);
        }

        [Fact]
        public async Task UnexpectedFailure_ShouldPropagateSoPayOSRetries()
        {
            _sender
                .Setup(s => s.Send(It.IsAny<FinishOrderCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("database is down"));

            Func<Task> act = () => Handle(PaidBody);

            await act.Should().ThrowAsync<InvalidOperationException>();
        }
    }
}
