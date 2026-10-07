using System.Diagnostics.Metrics;
using Application.Common;
using Application.Common.Behaviors;
using Application.Exceptions;
using Application.Features.Identity.Commands.ForgotPassword;
using Application.Features.Identity.Commands.Login;
using Application.Features.Identity.Commands.Logout;
using Application.Features.Users.Queries;
using Application.Interface;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace UnitTests.Application
{
    public class SecurityEventBehaviorTests : IDisposable
    {
        private readonly List<(LogLevel Level, string Message)> _logs = new();
        private readonly List<string> _events = new();
        private readonly MeterListener _listener = new();
        private readonly Mock<ICurrentUser> _currentUser = new();

        public SecurityEventBehaviorTests()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == PlatformMetrics.MeterName && instrument.Name == "education.security.events")
                    listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            {
                foreach (var tag in tags)
                    if (tag.Key == "event")
                        _events.Add((string)tag.Value!);
            });
            _listener.Start();
        }

        public void Dispose() => _listener.Dispose();

        private sealed class CapturingLogger<T> : ILogger<T>
        {
            private readonly List<(LogLevel, string)> _sink;
            public CapturingLogger(List<(LogLevel, string)> sink) => _sink = sink;
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => _sink.Add((logLevel, formatter(state, exception)));
        }

        private SecurityEventBehavior<TRequest, TResponse> Create<TRequest, TResponse>() where TRequest : notnull
            => new(new CapturingLogger<SecurityEventBehavior<TRequest, TResponse>>(_logs), _currentUser.Object);

        [Fact]
        public async Task AFailedSignIn_IsCountedAndLoggedAsAWarning_WithoutTheRawEmail()
        {
            var behavior = Create<LoginCommand, string>();

            var act = () => behavior.Handle(new LoginCommand { Email = "victim@example.com", Password = "x" },
                () => throw new AuthenticateException("Invalid credentials."), CancellationToken.None);

            await act.Should().ThrowAsync<AuthenticateException>();
            _events.Should().Equal("login_failed");
            _logs.Should().ContainSingle(l => l.Level == LogLevel.Warning && l.Message.Contains("login_failed"));
            _logs.Single().Message.Should().NotContain("victim@example.com");
        }

        [Fact]
        public async Task ALockedOutSignIn_IsADifferentEvent()
        {
            var behavior = Create<LoginCommand, string>();

            var act = () => behavior.Handle(new LoginCommand { Email = "victim@example.com" },
                () => throw new TooManyRequestsException("locked"), CancellationToken.None);

            await act.Should().ThrowAsync<TooManyRequestsException>();
            _events.Should().Equal("login_locked_out");
        }

        [Fact]
        public async Task ASuccessfulSignIn_IsAnInformationEvent_AndTheResponseIsUnchanged()
        {
            var behavior = Create<LoginCommand, string>();

            var response = await behavior.Handle(new LoginCommand { Email = "me@example.com" }, () => Task.FromResult("tokens"), CancellationToken.None);

            response.Should().Be("tokens");
            _events.Should().Equal("login_succeeded");
            _logs.Should().ContainSingle(l => l.Level == LogLevel.Information);
        }

        [Fact]
        public async Task ADatabaseOutage_IsNotAFailedSignIn()
        {
            var behavior = Create<LoginCommand, string>();

            var act = () => behavior.Handle(new LoginCommand { Email = "me@example.com" },
                () => throw new InvalidOperationException("database is down"), CancellationToken.None);

            await act.Should().ThrowAsync<InvalidOperationException>();
            _events.Should().BeEmpty();
        }

        [Fact]
        public async Task ARequestForAResetCode_IsRecorded()
        {
            await Create<ForgotPasswordCommand, Unit>().Handle(new ForgotPasswordCommand { Email = "me@example.com" },
                () => Task.FromResult(Unit.Value), CancellationToken.None);

            _events.Should().Equal("password_reset_requested");
        }

        [Fact]
        public async Task LoggingOutEverywhere_IsRecorded_ButLoggingOutOneDeviceIsNot()
        {
            var behavior = Create<LogoutCommand, Unit>();

            await behavior.Handle(new LogoutCommand { UserId = Guid.NewGuid() }, () => Task.FromResult(Unit.Value), CancellationToken.None);
            await behavior.Handle(new LogoutCommand { UserId = Guid.NewGuid(), RefreshToken = "device" }, () => Task.FromResult(Unit.Value), CancellationToken.None);

            _events.Should().Equal("all_sessions_revoked");
        }

        [Fact]
        public async Task OtherRequests_PassThroughUntouched()
        {
            var behavior = Create<ExportMyDataQuery, string>();

            var response = await behavior.Handle(new ExportMyDataQuery(), () => Task.FromResult("data"), CancellationToken.None);

            response.Should().Be("data");
            _events.Should().BeEmpty();
            _logs.Should().BeEmpty();
        }
    }
}
