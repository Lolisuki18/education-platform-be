using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Application.Common;
using Application.Options;
using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.Enum;
using FluentAssertions;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Seeds;
using Infrastructure.Services;
using Infrastructure.Services.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace UnitTests.InfrastructureTests
{
    public class JwtTokenServiceTests
    {
        private static JwtOptions Valid() => new()
        {
            SecretKey = "0123456789012345678901234567890123456789",
            Issuer = "issuer",
            Audience = "audience",
            ExpiryMinutes = 30
        };

        private static User NewUser() =>
            new(Guid.NewGuid(), "u@example.com", "Password123", "0123456789", "User", null, Role.Teacher, DateTime.UtcNow, true);

        [Fact]
        public void Token_ShouldCarryIdentityRoleAndExpiry()
        {
            var service = new JwtTokenService(Options.Create(Valid()));
            var user = NewUser();

            var token = new JwtSecurityTokenHandler().ReadJwtToken(service.GenerateToken(user));

            token.Issuer.Should().Be("issuer");
            token.Audiences.Should().Contain("audience");
            token.Claims.Should().Contain(c => c.Type == ClaimTypes.Role || c.Type == "role" && c.Value == "Teacher");
            token.Claims.Should().Contain(c => c.Value == user.UserID.ToString());
            token.ValidTo.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(30), TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void EveryToken_ShouldGetItsOwnId()
        {
            var service = new JwtTokenService(Options.Create(Valid()));
            var user = NewUser();
            var handler = new JwtSecurityTokenHandler();

            var first = handler.ReadJwtToken(service.GenerateToken(user)).Id;
            var second = handler.ReadJwtToken(service.GenerateToken(user)).Id;

            first.Should().NotBeNullOrEmpty();
            first.Should().NotBe(second);
        }

        [Fact]
        public void FractionalMinutes_ShouldBeHonoured()
        {
            var options = Valid();
            options.ExpiryMinutes = 0.5;
            var service = new JwtTokenService(Options.Create(options));

            var token = new JwtSecurityTokenHandler().ReadJwtToken(service.GenerateToken(NewUser()));

            token.ValidTo.Should().BeCloseTo(DateTime.UtcNow.AddSeconds(30), TimeSpan.FromSeconds(10));
        }

        [Fact]
        public void RefreshTokens_ShouldBeLongAndUnique()
        {
            var service = new JwtTokenService(Options.Create(Valid()));

            var tokens = Enumerable.Range(0, 20).Select(_ => service.GenerateRefreshToken()).ToList();

            tokens.Distinct().Should().HaveCount(20);
            tokens.Should().OnlyContain(t => t.Length >= 80);
        }

        [Fact]
        public void Options_ShouldRejectWeakOrMissingSettings()
        {
            new JwtOptions().Validate().Should().HaveCount(3); // key, issuer, audience

            var shortKey = Valid();
            shortKey.SecretKey = "too-short";
            shortKey.Validate().Should().ContainSingle(m => m.Contains("32 bytes"));

            var noExpiry = Valid();
            noExpiry.ExpiryMinutes = 0;
            noExpiry.Validate().Should().ContainSingle(m => m.Contains("ExpiryMinutes"));

            Valid().Validate().Should().BeEmpty();
        }

        [Fact]
        public void Validator_ShouldFailStartupWithEveryProblemListed()
        {
            var result = new JwtOptionsValidator().Validate(null, new JwtOptions());

            result.Failed.Should().BeTrue();
            result.Failures.Should().HaveCount(3);
        }
    }

    public class AfterCommitQueueTests
    {
        [Fact]
        public async Task Actions_ShouldRunOnceInOrder_ThenBeForgotten()
        {
            var queue = new AfterCommitQueue(NullLogger<AfterCommitQueue>.Instance);
            var log = new List<string>();
            queue.Enqueue(() => { log.Add("a"); return Task.CompletedTask; });
            queue.Enqueue(() => { log.Add("b"); return Task.CompletedTask; });

            await queue.RunAsync();
            await queue.RunAsync();

            log.Should().Equal("a", "b");
        }

        [Fact]
        public async Task FailingAction_ShouldNotStopTheOthers()
        {
            var queue = new AfterCommitQueue(NullLogger<AfterCommitQueue>.Instance);
            var ran = false;
            queue.Enqueue(() => throw new InvalidOperationException("smtp is down"));
            queue.Enqueue(() => { ran = true; return Task.CompletedTask; });

            await queue.RunAsync();

            ran.Should().BeTrue();
        }

        [Fact]
        public async Task Clear_ShouldDropWorkOfARolledBackTransaction()
        {
            var queue = new AfterCommitQueue(NullLogger<AfterCommitQueue>.Instance);
            var ran = false;
            queue.Enqueue(() => { ran = true; return Task.CompletedTask; });

            queue.Clear();
            await queue.RunAsync();

            ran.Should().BeFalse();
        }
    }

    public class EmailQueueTests
    {
        [Fact]
        public async Task QueuedService_ShouldOnlyEnqueueAndNeverTouchSmtp()
        {
            var queue = new EmailQueue(NullLogger<EmailQueue>.Instance);
            var service = new QueuedEmailService(queue);

            await service.SendVerificationEmailAsync("a@b.c", "123456");
            await service.SendEmailAsync("d@e.f", "Hello", "<p>Body</p>");

            queue.Reader.TryRead(out var first).Should().BeTrue();
            first!.To.Should().Be("a@b.c");
            first.Body.Should().Contain("123456");
            queue.Reader.TryRead(out var second).Should().BeTrue();
            second!.Subject.Should().Be("Hello");
        }

        [Fact]
        public async Task Dispatcher_ShouldSendEverythingThatIsQueuedBeforeShutdown()
        {
            var queue = new EmailQueue(NullLogger<EmailQueue>.Instance);
            var sender = new Mock<IEmailSender>();
            var sent = new List<string>();
            var firstSendStarted = new TaskCompletionSource();
            var releaseFirstSend = new TaskCompletionSource();

            sender.Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
                .Returns(async (EmailMessage m, CancellationToken _) =>
                {
                    if (sent.Count == 0)
                    {
                        firstSendStarted.SetResult();
                        await releaseFirstSend.Task;
                    }
                    sent.Add(m.To);
                });
            var dispatcher = new EmailDispatchService(queue, sender.Object, NullLogger<EmailDispatchService>.Instance);

            queue.Enqueue(new EmailMessage("a@b.c", "s", "b"));
            queue.Enqueue(new EmailMessage("d@e.f", "s", "b"));
            queue.Enqueue(new EmailMessage("g@h.i", "s", "b"));
            await dispatcher.StartAsync(CancellationToken.None);
            await firstSendStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));

            // Shutdown is requested while the first message is still being sent and two more are waiting
            var stopping = dispatcher.StopAsync(CancellationToken.None);
            releaseFirstSend.SetResult();
            await stopping.WaitAsync(TimeSpan.FromSeconds(10));

            sent.Should().Equal("a@b.c", "d@e.f", "g@h.i");
        }

        [Fact]
        public async Task Dispatcher_ShouldRetryAFailedSend()
        {
            var queue = new EmailQueue(NullLogger<EmailQueue>.Instance);
            var sender = new Mock<IEmailSender>();
            var attempts = 0;
            sender.Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
                .Returns(() => ++attempts < 2 ? throw new InvalidOperationException("temporary") : Task.CompletedTask);
            var dispatcher = new EmailDispatchService(queue, sender.Object, NullLogger<EmailDispatchService>.Instance);

            await dispatcher.SendWithRetryAsync(new EmailMessage("a@b.c", "s", "b"), CancellationToken.None);

            attempts.Should().Be(2);
        }

        [Fact]
        public async Task Dispatcher_ShouldGiveUpWithoutThrowingWhenShuttingDown()
        {
            var queue = new EmailQueue(NullLogger<EmailQueue>.Instance);
            var sender = new Mock<IEmailSender>();
            sender.Setup(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("smtp is down"));
            var dispatcher = new EmailDispatchService(queue, sender.Object, NullLogger<EmailDispatchService>.Instance);
            using var stopping = new CancellationTokenSource();
            stopping.Cancel();

            Func<Task> act = () => dispatcher.SendWithRetryAsync(new EmailMessage("a@b.c", "s", "b"), stopping.Token);

            await act.Should().NotThrowAsync();
            sender.Verify(s => s.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    public class SearchPatternTests
    {
        [Theory]
        [InlineData("Physics", "%physics%")]
        [InlineData("  Physics  ", "%physics%")]
        [InlineData("100%", "%100\\%%")]
        [InlineData("a_b", "%a\\_b%")]
        [InlineData("a\\b", "%a\\\\b%")]
        public void Contains_ShouldLowercaseTrimAndEscapeWildcards(string term, string expected)
        {
            SearchPattern.Contains(term).Should().Be(expected);
        }
    }

    public class SeederTests
    {
        private static EducationPlatformDBContext NewDb() =>
            new(new DbContextOptionsBuilder<EducationPlatformDBContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        private sealed class CapturingLogger : ILogger
        {
            public List<(LogLevel Level, string Message)> Entries { get; } = new();
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => Entries.Add((logLevel, formatter(state, exception)));
        }

        [Fact]
        public async Task WithoutDemoData_ShouldSeedReferenceDataOnly_AndNoUsers()
        {
            await using var db = NewDb();

            await Seeder.SeedAsync(db, new SeedOptions { DemoData = false }, NullLogger.Instance);

            (await db.Grades.CountAsync()).Should().BeGreaterThan(0);
            (await db.Subjects.CountAsync()).Should().BeGreaterThan(0);
            (await db.Users.CountAsync()).Should().Be(0, "no account may exist unless somebody configured it");
            (await db.Courses.CountAsync()).Should().Be(0);
            (await db.Orders.CountAsync()).Should().Be(0);
        }

        [Fact]
        public async Task WithoutAdminConfiguration_ShouldWarnInsteadOfInventingAPassword()
        {
            await using var db = NewDb();
            var logger = new CapturingLogger();

            await Seeder.SeedAsync(db, new SeedOptions(), logger);

            (await db.Users.CountAsync()).Should().Be(0);
            logger.Entries.Should().Contain(e => e.Level == LogLevel.Warning && e.Message.Contains("No administrator exists"));
        }

        [Fact]
        public async Task ConfiguredAdmin_ShouldBeCreatedOnce()
        {
            await using var db = NewDb();
            var options = new SeedOptions
            {
                Admin = new AdminSeedOptions { Email = "boss@example.com", Password = "Str0ngAdminPass" }
            };

            await Seeder.SeedAsync(db, options, NullLogger.Instance);
            await Seeder.SeedAsync(db, options, NullLogger.Instance);

            var admin = await db.Users.SingleAsync();
            admin.Role.Should().Be(Role.Admin);
            admin.IsVerified.Should().BeTrue();
            admin.Password.Verify("Str0ngAdminPass").Should().BeTrue();
        }

        [Fact]
        public async Task AdminConfiguration_ShouldBeIgnoredWhenAnAdminAlreadyExists()
        {
            await using var db = NewDb();
            db.Users.Add(new User(Guid.NewGuid(), "existing@example.com", "Password123", "0000000009", "Existing", null, Role.Admin, null, true));
            await db.SaveChangesAsync();

            await Seeder.SeedAsync(db, new SeedOptions
            {
                Admin = new AdminSeedOptions { Email = "other@example.com", Password = "Str0ngAdminPass" }
            }, NullLogger.Instance);

            (await db.Users.SingleAsync()).Email.Should().Be("existing@example.com");
        }

        [Fact]
        public async Task AdminWithTheOldPublicPassword_ShouldBeReportedLoudly()
        {
            await using var db = NewDb();
            var logger = new CapturingLogger();
            db.Users.Add(new User(Guid.NewGuid(), "old-admin@example.com", "Password123", "0000000008", "Old Admin", null, Role.Admin, null, true));
            await db.SaveChangesAsync();

            // The old seeder used a digits-only password that today's policy would refuse, so plant its hash directly
            var stored = await db.Users.SingleAsync();
            var hash = BCrypt.Net.BCrypt.HashPassword(AdminSeeder.LegacyDefaultPassword);
            typeof(Domain.IdentityManagement.ValueObject.Password)
                .GetField("<Hash>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .SetValue(stored.Password, hash);
            db.Entry(stored.Password).State = EntityState.Modified;
            await db.SaveChangesAsync();

            await AdminSeeder.WarnAboutLegacyPasswordsAsync(db, logger);

            logger.Entries.Should().Contain(e => e.Level == LogLevel.Critical && e.Message.Contains("o***@example.com") && !e.Message.Contains("old-admin"));
        }

        [Fact]
        public async Task AdminWithAnOwnPassword_ShouldNotBeReported()
        {
            await using var db = NewDb();
            var logger = new CapturingLogger();
            db.Users.Add(new User(Guid.NewGuid(), "admin@example.com", "Password123", "0000000007", "Admin", null, Role.Admin, null, true));
            await db.SaveChangesAsync();

            await AdminSeeder.WarnAboutLegacyPasswordsAsync(db, logger);

            logger.Entries.Should().NotContain(e => e.Level == LogLevel.Critical);
        }
    }
}
