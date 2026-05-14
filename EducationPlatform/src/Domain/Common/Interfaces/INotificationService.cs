namespace Domain.Common.Interfaces
{
    /// <summary>
    /// Port (Hexagonal Architecture): Defines how the Domain/Application
    /// can send notifications to users.
    ///
    /// Lives in Domain so BOTH Application handlers AND Infrastructure implementations
    /// can reference it without creating a circular dependency.
    ///
    /// The concrete adapter (LogNotificationService, EmailNotificationService, etc.)
    /// lives in Infrastructure and is wired via DI — Domain never knows which one runs.
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Sends a notification message to a specific user.
        /// </summary>
        /// <param name="userId">The target user's ID as a string.</param>
        /// <param name="title">Short notification title / subject line.</param>
        /// <param name="message">Full notification body.</param>
        Task SendAsync(
            string userId,
            string title,
            string message,
            CancellationToken cancellationToken = default);
    }
}
