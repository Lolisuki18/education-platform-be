using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Common.Behaviors;
using Application.Interface;
using Application.Options;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace UnitTests.Application
{
    public class CachingBehaviorTests
    {
        public class ReportQuery : IRequest<string>, ICachedQuery
        {
            public int Year { get; set; }
        }

        public class PlainQuery : IRequest<string>
        {
        }

        private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
        private readonly Mock<ICurrentUser> _user = new();

        private CachingBehavior<TRequest, string> Create<TRequest>(bool enabled = true) where TRequest : IRequest<string> =>
            new(_cache, _user.Object, Options.Create(new CachingOptions { Enabled = enabled }));

        private static RequestHandlerDelegate<string> Counting(Action onCall) => () =>
        {
            onCall();
            return Task.FromResult("answer");
        };

        [Fact]
        public async Task TheSameQuery_IsComputedOnce()
        {
            var calls = 0;
            var behavior = Create<ReportQuery>();

            await behavior.Handle(new ReportQuery { Year = 2026 }, Counting(() => calls++), CancellationToken.None);
            await behavior.Handle(new ReportQuery { Year = 2026 }, Counting(() => calls++), CancellationToken.None);

            calls.Should().Be(1);
        }

        [Fact]
        public async Task DifferentParameters_AreCachedSeparately()
        {
            var calls = 0;
            var behavior = Create<ReportQuery>();

            await behavior.Handle(new ReportQuery { Year = 2025 }, Counting(() => calls++), CancellationToken.None);
            await behavior.Handle(new ReportQuery { Year = 2026 }, Counting(() => calls++), CancellationToken.None);

            calls.Should().Be(2);
        }

        [Fact]
        public async Task DifferentUsers_NeverShareAnAnswer()
        {
            var calls = 0;
            var behavior = Create<ReportQuery>();

            _user.Setup(u => u.Id).Returns(Guid.NewGuid());
            await behavior.Handle(new ReportQuery(), Counting(() => calls++), CancellationToken.None);

            _user.Setup(u => u.Id).Returns(Guid.NewGuid());
            await behavior.Handle(new ReportQuery(), Counting(() => calls++), CancellationToken.None);

            calls.Should().Be(2);
        }

        [Fact]
        public async Task QueriesThatAreNotMarked_AreNeverCached()
        {
            var calls = 0;
            var behavior = Create<PlainQuery>();

            await behavior.Handle(new PlainQuery(), Counting(() => calls++), CancellationToken.None);
            await behavior.Handle(new PlainQuery(), Counting(() => calls++), CancellationToken.None);

            calls.Should().Be(2);
        }

        [Fact]
        public async Task WhenSwitchedOff_EverythingIsComputedEveryTime()
        {
            var calls = 0;
            var behavior = Create<ReportQuery>(enabled: false);

            await behavior.Handle(new ReportQuery(), Counting(() => calls++), CancellationToken.None);
            await behavior.Handle(new ReportQuery(), Counting(() => calls++), CancellationToken.None);

            calls.Should().Be(2);
        }

        [Fact]
        public async Task ConcurrentIdenticalRequests_ShareOneComputation()
        {
            var calls = 0;
            var behavior = Create<ReportQuery>();
            var gate = new TaskCompletionSource();

            RequestHandlerDelegate<string> slow = async () =>
            {
                Interlocked.Increment(ref calls);
                await gate.Task;
                return "answer";
            };

            var tasks = new[]
            {
                behavior.Handle(new ReportQuery(), slow, CancellationToken.None),
                behavior.Handle(new ReportQuery(), slow, CancellationToken.None),
                behavior.Handle(new ReportQuery(), slow, CancellationToken.None)
            };

            await Task.Delay(100);
            gate.SetResult();
            var results = await Task.WhenAll(tasks);

            calls.Should().Be(1);
            results.Should().OnlyContain(r => r == "answer");
        }

        [Fact]
        public void StatisticsReports_AreMarkedAsCacheable()
        {
            typeof(global::Application.Features.Statistics.Queries.GetSummaryStatistics.GetSummaryStatisticsQuery)
                .Should().Implement<ICachedQuery>();
            typeof(global::Application.Features.Statistics.Queries.GetTopPerformance.GetTopPerformanceQuery)
                .Should().Implement<ICachedQuery>();
        }
    }
}
