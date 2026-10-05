using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Common;
using Application.Features.Orders.Commands.CancelExpiredOrders;
using Application.Features.Orders.Commands.ProcessPayOSWebhook;
using Application.Interface;
using Application.Options;
using Domain.Common.Interfaces;
using Domain.OrderManagement.Aggregate;
using Domain.OrderManagement.ValueObject;
using FluentAssertions;
using Infrastructure.Services;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace UnitTests.Application
{
    /// <summary>Records what the platform meter reports while the test runs.</summary>
    public sealed class MetricRecorder : IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly ConcurrentBag<(string Name, long Value, string? ResultTag)> _measurements = new();

        public MetricRecorder()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == PlatformMetrics.MeterName)
                    listener.EnableMeasurementEvents(instrument);
            };

            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            {
                string? result = null;
                foreach (var tag in tags)
                {
                    if (tag.Key is "result" or "kind")
                        result = tag.Value?.ToString();
                }

                _measurements.Add((instrument.Name, value, result));
            });

            _listener.Start();
        }

        public long Sum(string name, string? tag = null) =>
            _measurements.Where(m => m.Name == name && (tag == null || m.ResultTag == tag)).Sum(m => m.Value);

        public void Dispose() => _listener.Dispose();
    }

    public class MetricsTests
    {
        [Fact]
        public async Task CancellingExpiredOrders_IsCounted()
        {
            using var metrics = new MetricRecorder();
            var unitOfWork = new Mock<IUnitOfWork>();
            var repository = new Mock<IOrderRepository>();
            unitOfWork.Setup(u => u.GetRepository<IOrderRepository>()).Returns(repository.Object);

            Order Expired() => new(Guid.NewGuid(), Commission.Create(0.15m, 100m), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddHours(-2));
            repository.Setup(r => r.GetUnpaidOrdersCreatedBefore(It.IsAny<DateTime>(), It.IsAny<int>()))
                .ReturnsAsync(new List<Order> { Expired(), Expired() });
            repository.Setup(r => r.GetCouponsByIds(It.IsAny<IEnumerable<Guid>>())).ReturnsAsync(new List<Coupon>());

            await new CancelExpiredOrdersCommandHandler(unitOfWork.Object).Handle(new CancelExpiredOrdersCommand(), CancellationToken.None);

            metrics.Sum("education.orders.cancelled").Should().BeGreaterThanOrEqualTo(2);
        }

        [Fact]
        public async Task WebhookOutcomes_AreCountedByResult()
        {
            using var metrics = new MetricRecorder();
            var verifier = new Mock<IPayOSSignatureVerifier>();
            verifier.Setup(v => v.VerifyWebhookSignature(It.IsAny<IDictionary<string, string?>>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(false);
            var handler = new ProcessPayOSWebhookCommandHandler(
                verifier.Object, new Mock<ISender>().Object,
                Options.Create(new PayOSOptions { ChecksumKey = "k" }),
                NullLogger<ProcessPayOSWebhookCommandHandler>.Instance);

            await handler.Handle(new ProcessPayOSWebhookCommand { Body = "not json" }, CancellationToken.None);
            await handler.Handle(new ProcessPayOSWebhookCommand { Body = "{\"code\":\"00\",\"data\":{},\"signature\":\"x\"}" }, CancellationToken.None);

            metrics.Sum("education.payments.webhooks", "Malformed").Should().BeGreaterThanOrEqualTo(1);
            metrics.Sum("education.payments.webhooks", "InvalidSignature").Should().BeGreaterThanOrEqualTo(1);
        }

        [Fact]
        public void AnAccountLockout_IsCountedOnceWhenTheLimitIsReached()
        {
            using var metrics = new MetricRecorder();
            var tracker = new MemoryLoginAttemptTracker(new MemoryCache(new MemoryCacheOptions()));

            for (var i = 0; i < MemoryLoginAttemptTracker.MaxFailures + 3; i++)
                tracker.RegisterFailure("someone@example.com");

            metrics.Sum("education.auth.lockouts").Should().BeGreaterThanOrEqualTo(1);
        }
    }

    public class LogMaskTests
    {
        [Theory]
        [InlineData("jane.doe@gmail.com", "j***@gmail.com")]
        [InlineData("a@b.co", "a***@b.co")]
        [InlineData("", "")]
        [InlineData(null, "")]
        [InlineData("not-an-email", "***")]
        public void Email_KeepsTheDomainAndHidesTheRest(string? input, string expected)
        {
            LogMask.Email(input).Should().Be(expected);
        }

        [Fact]
        public void Scrub_MasksEveryAddressInsideFreeText()
        {
            var text = "User with email jane.doe@gmail.com already exists; also tried bob@example.org.";

            var scrubbed = LogMask.Scrub(text);

            scrubbed.Should().Be("User with email j***@gmail.com already exists; also tried b***@example.org.");
            scrubbed.Should().NotContain("jane.doe").And.NotContain("bob@");
        }

        [Fact]
        public void Scrub_LeavesOtherTextAlone()
        {
            LogMask.Scrub("Course with ID: 123 is not found").Should().Be("Course with ID: 123 is not found");
            LogMask.Scrub(null).Should().BeEmpty();
        }
    }
}
