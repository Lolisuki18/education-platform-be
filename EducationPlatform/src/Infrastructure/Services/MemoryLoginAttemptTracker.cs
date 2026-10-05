using Application.Interface;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Services
{
    /// <summary>
    /// In-process implementation. With several API instances each one counts on its own,
    /// so swap in a distributed store if strict account lockout is required.
    /// </summary>
    public class MemoryLoginAttemptTracker : ILoginAttemptTracker
    {
        public const int MaxFailures = 5;
        public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(10);

        private readonly IMemoryCache _cache;

        public MemoryLoginAttemptTracker(IMemoryCache cache)
        {
            _cache = cache;
        }

        public bool IsLockedOut(string key)
        {
            return _cache.TryGetValue(CacheKey(key), out int failures) && failures >= MaxFailures;
        }

        public void RegisterFailure(string key)
        {
            var cacheKey = CacheKey(key);
            _cache.TryGetValue(cacheKey, out int failures);

            // Every failure extends the window, so the lock lasts LockoutDuration after the last attempt.
            _cache.Set(cacheKey, failures + 1, LockoutDuration);
        }

        public void Reset(string key)
        {
            _cache.Remove(CacheKey(key));
        }

        private static string CacheKey(string key) => $"LoginFailures:{key.Trim().ToLowerInvariant()}";
    }
}
