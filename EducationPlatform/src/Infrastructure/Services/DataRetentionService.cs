using Application.Options;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services
{
    /// <summary>
    /// Housekeeping that keeps append-only tables from growing forever: dead refresh sessions, old audit
    /// log rows, and registrations nobody ever verified (which also keep a phone number and e-mail address reserved).
    /// </summary>
    public class DataRetentionService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly RetentionOptions _options;
        private readonly ILogger<DataRetentionService> _logger;

        public DataRetentionService(
            IServiceScopeFactory scopeFactory,
            IOptions<RetentionOptions> options,
            ILogger<DataRetentionService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
                return;

            // Let the startup migration finish and the instance warm up first
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<EducationPlatformDBContext>();
                    await CleanAsync(db, DateTime.UtcNow, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Data retention run failed.");
                }

                await Task.Delay(Interval, stoppingToken);
            }
        }

        internal async Task CleanAsync(EducationPlatformDBContext db, DateTime now, CancellationToken cancellationToken)
        {
            var sessionCutoff = now.AddDays(-_options.RefreshSessionGraceDays);
            var sessions = await db.RefreshSessions
                .Where(s => s.ExpiresAt < sessionCutoff || (s.RevokedAt != null && s.RevokedAt < sessionCutoff))
                .ExecuteDeleteAsync(cancellationToken);

            var auditCutoff = now.AddDays(-_options.AuditLogDays);
            var auditLogs = await db.AuditLogs
                .Where(a => a.Timestamp < auditCutoff)
                .ExecuteDeleteAsync(cancellationToken);

            var notificationCutoff = now.AddDays(-_options.NotificationDays);
            var notifications = await db.Notifications
                .Where(n => n.CreatedAt < notificationCutoff)
                .ExecuteDeleteAsync(cancellationToken);

            var unverifiedCutoff = now.AddDays(-_options.UnverifiedUserDays);
            var users = await db.Users
                .Where(u => !u.IsVerified && u.CreatedAt < unverifiedCutoff)
                .ExecuteDeleteAsync(cancellationToken);

            if (sessions + auditLogs + users + notifications > 0)
            {
                _logger.LogInformation(
                    "Data retention removed {Sessions} refresh sessions, {AuditLogs} audit log rows, {Notifications} notifications and {Users} unverified users.",
                    sessions, auditLogs, notifications, users);
            }
        }
    }
}
