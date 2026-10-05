using System;
using FluentAssertions;
using Application.Interface;
using Infrastructure.Services;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace UnitTests.InfrastructureTests
{
    public class MemoryLoginAttemptTrackerTests
    {
        private readonly MemoryLoginAttemptTracker _tracker = new(new MemoryCache(new MemoryCacheOptions()));

        [Fact]
        public void BelowTheLimit_ShouldNotLockOut()
        {
            for (var i = 0; i < MemoryLoginAttemptTracker.MaxFailures - 1; i++)
                _tracker.RegisterFailure("a@b.c");

            _tracker.IsLockedOut("a@b.c").Should().BeFalse();
        }

        [Fact]
        public void ReachingTheLimit_ShouldLockOutThatAccountOnly()
        {
            for (var i = 0; i < MemoryLoginAttemptTracker.MaxFailures; i++)
                _tracker.RegisterFailure("a@b.c");

            _tracker.IsLockedOut("a@b.c").Should().BeTrue();
            _tracker.IsLockedOut("someone-else@b.c").Should().BeFalse();
        }

        [Fact]
        public void Keys_ShouldIgnoreCaseAndWhitespace()
        {
            for (var i = 0; i < MemoryLoginAttemptTracker.MaxFailures; i++)
                _tracker.RegisterFailure("  A@B.C ");

            _tracker.IsLockedOut("a@b.c").Should().BeTrue();
        }

        [Fact]
        public void Reset_ShouldClearTheCounter()
        {
            for (var i = 0; i < MemoryLoginAttemptTracker.MaxFailures; i++)
                _tracker.RegisterFailure("a@b.c");

            _tracker.Reset("a@b.c");

            _tracker.IsLockedOut("a@b.c").Should().BeFalse();
        }
    }

    public class MemoryUserActivityCacheTests
    {
        private readonly MemoryUserActivityCache _cache = new(new MemoryCache(new MemoryCacheOptions()));

        [Fact]
        public void UnknownUser_ShouldBeAMiss()
        {
            _cache.TryGet(Guid.NewGuid(), out _).Should().BeFalse();
        }

        [Fact]
        public void StoredStatus_ShouldBeReturned_UntilInvalidated()
        {
            var id = Guid.NewGuid();
            _cache.Set(id, new UserStatus(true, "Teacher"));

            _cache.TryGet(id, out var status).Should().BeTrue();
            status.IsActive.Should().BeTrue();
            status.Role.Should().Be("Teacher");

            _cache.Invalidate(id);

            _cache.TryGet(id, out _).Should().BeFalse();
        }
    }
}
