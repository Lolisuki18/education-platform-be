using Domain.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    /// <summary>
    /// Logging-based implementation of INotificationService.
    ///
    /// Strategy: Log notifications to the console/structured log output.
    /// This serves as the functional stub for the current phase.
    /// Swap this with a real Email/FCM/SignalR implementation later
    /// without touching ANY Application-layer code — just change this class
    /// and its DI registration.
    /// </summary>
    public class LogNotificationService : INotificationService
    {
        private readonly ILogger<LogNotificationService> _logger;

        public LogNotificationService(ILogger<LogNotificationService> logger)
        {
            _logger = logger;
        }

        public Task SendAsync(
            string userId,
            string title,
            string message,
            CancellationToken cancellationToken = default)
        {
            // In production this would call FCM, SendGrid, SignalR hub, etc.
            // For now we emit a structured log so the Event Handler behavior is verifiable
            // in tests and development without any external dependencies.
            _logger.LogInformation(
                "[NOTIFICATION] → UserID: {UserId} | Title: {Title} | Message: {Message}",
                userId, title, message);

            return Task.CompletedTask;
        }
    }
}
