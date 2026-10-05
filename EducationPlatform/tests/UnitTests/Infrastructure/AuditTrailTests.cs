using System;
using System.Linq;
using System.Threading.Tasks;
using Domain.AuditManagement.Aggregate;
using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.Enum;
using Domain.NotificationManagement.Aggregate;
using FluentAssertions;
using Infrastructure.Implementation;
using Infrastructure.Persistence;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace UnitTests.InfrastructureTests
{
    public class AuditTrailTests
    {
        private static (UnitOfWork Uow, EducationPlatformDBContext Db) Create()
        {
            var db = new EducationPlatformDBContext(
                new DbContextOptionsBuilder<EducationPlatformDBContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

            var services = new ServiceCollection();
            services.AddSingleton(db);
            services.AddScoped<IAuditRepository, AuditLogRepository>();

            return (new UnitOfWork(db, services.BuildServiceProvider(), new AfterCommitQueue(NullLogger<AfterCommitQueue>.Instance)), db);
        }

        [Fact]
        public async Task PasswordHashes_AreNeverWrittenToTheAuditTrail()
        {
            var (uow, db) = Create();
            db.Users.Add(new User(Guid.NewGuid(), "u@example.com", "Password123", "0123456789", "User", null, Role.Student, DateTime.UtcNow, true));

            await uow.CommitAsync("tester");

            var logs = await db.AuditLogs.ToListAsync();
            logs.Should().Contain(l => l.EntityName == "User");
            logs.Should().NotContain(l => l.EntityName == "Password");
            logs.Select(l => l.NewValue).Should().NotContain(v => v != null && v.Contains("$2"), "a BCrypt hash starts with $2");
        }

        [Fact]
        public async Task RefreshSessionsAndNotifications_AreNotAudited()
        {
            var (uow, db) = Create();
            var user = new User(Guid.NewGuid(), "u@example.com", "Password123", "0123456789", "User", null, Role.Student, DateTime.UtcNow, true);
            user.IssueRefreshToken("secret-refresh-token", TimeSpan.FromDays(7));
            db.Users.Add(user);
            db.Notifications.Add(new Notification(user.UserID, "Hello", "World"));

            await uow.CommitAsync("tester");

            var names = (await db.AuditLogs.ToListAsync()).Select(l => l.EntityName).ToList();
            names.Should().Contain("User");
            names.Should().NotContain(new[] { "RefreshSession", "Notification", "Password", "AuditLog" });
        }
    }
}
