using Domain.Exceptions;

namespace Domain.NotificationManagement.Aggregate
{
    /// <summary>A message for one user, kept so it can be read later even if they were offline when it was sent.</summary>
    public class Notification
    {
        public const int MaxTitleLength = 200;
        public const int MaxMessageLength = 2000;

        public Guid NotificationID { get; private set; }
        public Guid UserID { get; private set; }
        public string Title { get; private set; } = string.Empty;
        public string Message { get; private set; } = string.Empty;
        public DateTime CreatedAt { get; private set; }
        public DateTime? ReadAt { get; private set; }

        public bool IsRead => ReadAt.HasValue;

        protected Notification() { }

        public Notification(Guid userId, string title, string message)
        {
            if (userId == Guid.Empty)
                throw new DomainException("User ID cannot be empty");

            if (string.IsNullOrWhiteSpace(title))
                throw new DomainException("Notification title is required");

            NotificationID = Guid.NewGuid();
            UserID = userId;
            Title = Truncate(title.Trim(), MaxTitleLength);
            Message = Truncate((message ?? string.Empty).Trim(), MaxMessageLength);
            CreatedAt = DateTime.UtcNow;
        }

        public void MarkAsRead()
        {
            ReadAt ??= DateTime.UtcNow;
        }

        private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
    }
}
