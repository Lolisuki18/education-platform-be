using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Options;
using Domain.AuditManagement.Aggregate;
using Domain.CourseManagement.Aggregate;
using Domain.IdentityManagement.Aggregate;
using Domain.NotificationManagement.Aggregate;
using Domain.OrderManagement.Aggregate;
using Domain.OrderManagement.Enum;
using Domain.OrderManagement.ValueObject;
using FluentAssertions;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace IntegrationTests.Controllers
{
    /// <summary>The housekeeping jobs run only in production, so nothing else ever exercises their SQL.</summary>
    public class BackgroundJobTests : IntegrationTestBase
    {
        public BackgroundJobTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        [Fact]
        public async Task DataRetention_RemovesWhatHasOutlivedItsUse_AndKeepsTheRest()
        {
            var (unverifiedId, verifiedId) = await ExecuteDbContextAsync(async db =>
            {
                var unverified = new User(Guid.NewGuid(), "never.verified@example.com", "Password123!", "0966666666", "Never Verified", null,
                    Domain.IdentityManagement.Enum.Role.Student, null);
                unverified.IssueRefreshToken("expiring-token", TimeSpan.FromMinutes(1));
                db.Set<User>().Add(unverified);

                var student = await db.Set<User>().Include(u => u.RefreshSessions).FirstAsync(u => u.Email == "student@example.com");
                student.IssueRefreshToken("short-lived", TimeSpan.FromMinutes(1));
                student.IssueRefreshToken("long-lived", TimeSpan.FromDays(900));

                db.Set<AuditLog>().Add(new AuditLog("Course", "Added", null, null, "{}"));
                db.Set<Notification>().Add(new Notification(student.UserID, "Old news", "..."));
                await db.SaveChangesAsync();
                return (unverified.UserID, student.UserID);
            });

            var service = new DataRetentionService(
                Factory.Services.GetRequiredService<IServiceScopeFactory>(),
                Options.Create(new RetentionOptions()),
                NullLogger<DataRetentionService>.Instance);

            // Defaults: sessions 7 days, audit 365, notifications 90, unverified users 7. Look at it 400 days from now.
            await ExecuteDbContextAsync(db => service.CleanAsync(db, DateTime.UtcNow.AddDays(400), CancellationToken.None));

            await ExecuteDbContextAsync(async db =>
            {
                (await db.Set<User>().AnyAsync(u => u.UserID == unverifiedId)).Should().BeFalse("nobody verified that address");
                (await db.Set<User>().AnyAsync(u => u.UserID == verifiedId)).Should().BeTrue();

                var sessions = await db.RefreshSessions.Where(s => s.UserID == verifiedId).ToListAsync();
                sessions.Should().ContainSingle("only the session that is still valid survives");

                (await db.Set<AuditLog>().AnyAsync()).Should().BeFalse();
                (await db.Notifications.AnyAsync(n => n.UserID == verifiedId)).Should().BeFalse();
            });
        }

        [Fact]
        public async Task DataRetention_KeepsRecentRows()
        {
            await ExecuteDbContextAsync(async db =>
            {
                var student = await db.Set<User>().FirstAsync(u => u.Email == "student@example.com");
                db.Set<AuditLog>().Add(new AuditLog("Course", "Added", null, null, "{}"));
                db.Set<Notification>().Add(new Notification(student.UserID, "Fresh", "..."));
                db.Set<User>().Add(new User(Guid.NewGuid(), "just.registered@example.com", "Password123!", "0977777777", "New", null,
                    Domain.IdentityManagement.Enum.Role.Student, null));
                await db.SaveChangesAsync();
            });

            var service = new DataRetentionService(
                Factory.Services.GetRequiredService<IServiceScopeFactory>(),
                Options.Create(new RetentionOptions()),
                NullLogger<DataRetentionService>.Instance);

            await ExecuteDbContextAsync(db => service.CleanAsync(db, DateTime.UtcNow, CancellationToken.None));

            await ExecuteDbContextAsync(async db =>
            {
                (await db.Set<AuditLog>().CountAsync()).Should().Be(1);
                (await db.Notifications.CountAsync()).Should().Be(1);
                (await db.Set<User>().AnyAsync(u => u.Email == "just.registered@example.com")).Should().BeTrue();
            });
        }

        [Fact]
        public async Task TheExpiredOrderSweeper_CancelsStaleOrders_AndGivesTheirCouponsBack()
        {
            var (staleId, freshId, couponId) = await ExecuteDbContextAsync(async db =>
            {
                var student = await db.Set<User>().FirstAsync(u => u.Email == "student@example.com");
                var course = await db.Set<Course>().FirstAsync(c => c.Title == "Math algebra");
                var coupon = await db.Coupons.FirstAsync();
                coupon.MarkAsUsed();

                // Well past the payment window and its grace period
                var stale = new Order(Guid.NewGuid(), Commission.Create(0.15m, 40000m), student.UserID, course.CourseID,
                    DateTime.UtcNow.AddHours(-3), new[] { coupon.CouponID });
                var otherCourse = await db.Set<Course>().FirstAsync(c => c.Title == "Math geometry");
                var fresh = new Order(Guid.NewGuid(), Commission.Create(0.15m, 50000m), student.UserID, otherCourse.CourseID, null);
                db.Orders.AddRange(stale, fresh);
                await db.SaveChangesAsync();
                return (stale.OrderID, fresh.OrderID, coupon.CouponID);
            });

            var sweeper = new ExpiredOrderCleanupService(
                Factory.Services.GetRequiredService<IServiceScopeFactory>(),
                Factory.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>(),
                NullLogger<ExpiredOrderCleanupService>.Instance);

            await sweeper.RunOnceAsync(CancellationToken.None);

            await ExecuteDbContextAsync(async db =>
            {
                (await db.Orders.AsNoTracking().FirstAsync(o => o.OrderID == staleId)).Status.Should().Be(OrderStatus.Cancelled);
                (await db.Orders.AsNoTracking().FirstAsync(o => o.OrderID == freshId)).Status.Should().Be(OrderStatus.Created);

                var coupon = await db.Coupons.AsNoTracking().FirstAsync(c => c.CouponID == couponId);
                coupon.CurrentUsage.Should().Be(0);
                coupon.IsUsed.Should().BeFalse();
            });
        }
    }
}
