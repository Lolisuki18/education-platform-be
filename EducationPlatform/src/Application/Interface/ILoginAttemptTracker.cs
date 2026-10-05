namespace Application.Interface
{
    /// <summary>
    /// Tracks failed logins per account so a single account cannot be brute-forced from many IP addresses.
    /// </summary>
    public interface ILoginAttemptTracker
    {
        bool IsLockedOut(string key);

        void RegisterFailure(string key);

        void Reset(string key);
    }
}
