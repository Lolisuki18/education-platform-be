namespace Application.Options
{
    public class NotificationOptions
    {
        public const string SectionName = "Notifications";

        /// <summary>Also e-mail every notification, so people who never open the app still hear about it.</summary>
        public bool EmailEnabled { get; set; } = true;
    }
}
