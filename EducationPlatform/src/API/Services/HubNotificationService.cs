using System.Net;
using Application.Features.Notifications;
using Application.Interface;
using Application.Options;
using Domain.Common.Interfaces;
using Domain.NotificationManagement.Aggregate;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using API.Hubs;

namespace API.Services
{
    /// <summary>
    /// Stores a notification, pushes it to the user's open connections over SignalR and e-mails it.
    /// Domain event handlers call <see cref="SendAsync"/> while the triggering change is still being saved, so
    /// delivery is deferred until that change has been committed: nobody hears about something that was rolled back.
    /// </summary>
    public class HubNotificationService : INotificationService
    {
        private readonly IAfterCommitQueue _afterCommit;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<CourseHub> _hub;
        private readonly NotificationOptions _options;
        private readonly ILogger<HubNotificationService> _logger;

        public HubNotificationService(
            IAfterCommitQueue afterCommit,
            IServiceScopeFactory scopeFactory,
            IHubContext<CourseHub> hub,
            IOptions<NotificationOptions> options,
            ILogger<HubNotificationService> logger)
        {
            _afterCommit = afterCommit;
            _scopeFactory = scopeFactory;
            _hub = hub;
            _options = options.Value;
            _logger = logger;
        }

        public Task SendAsync(string userId, string title, string message, CancellationToken cancellationToken = default)
        {
            if (!Guid.TryParse(userId, out var id))
            {
                _logger.LogWarning("Notification skipped: {UserId} is not a valid user id.", userId);
                return Task.CompletedTask;
            }

            _afterCommit.Enqueue(() => DeliverAsync(id, title, message));
            return Task.CompletedTask;
        }

        private async Task DeliverAsync(Guid userId, string title, string message)
        {
            // A scope of its own: the request's DbContext is busy finishing the change that caused this
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<EducationPlatformDBContext>();

            var user = await db.Users.AsNoTracking()
                .Where(u => u.UserID == userId && u.IsActive)
                .Select(u => new { u.Email })
                .FirstOrDefaultAsync();

            if (user == null)
                return;

            var notification = new Notification(userId, title, message);
            db.Notifications.Add(notification);
            await db.SaveChangesAsync();

            await _hub.Clients.Group(CourseHub.UserGroup(userId)).SendAsync("Notification", new
            {
                notification.NotificationID,
                notification.Title,
                notification.Message,
                notification.CreatedAt,
                IsRead = false
            });

            if (_options.EmailEnabled)
            {
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                await emailService.SendEmailAsync(user.Email, notification.Title, BuildEmailBody(notification));
            }
        }

        private static string BuildEmailBody(Notification n)
        {
            var title = WebUtility.HtmlEncode(n.Title);
            var message = WebUtility.HtmlEncode(n.Message);

            return $@"<!DOCTYPE html>
<html>
<body style='font-family:Segoe UI, Tahoma, sans-serif;background:#f9f9f9;padding:40px'>
  <div style='max-width:600px;margin:auto;background:#fff;padding:30px;border-radius:10px;border:1px solid #e1e2ed'>
    <h2 style='color:#004ac6;margin-top:0'>{title}</h2>
    <p style='line-height:1.6;color:#434655'>{message}</p>
    <p style='font-size:12px;color:#888'>You are receiving this because of activity on your Education Platform account.</p>
  </div>
</body>
</html>";
        }
    }
}
