namespace Domain.Common.Interfaces
{
    public interface INotificationService
    {
        Task SendAsync(
            string userId,
            string title,
            string message,
            CancellationToken cancellationToken = default);
    }
}
