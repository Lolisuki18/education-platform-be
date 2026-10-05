using Application.Interface;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Services
{
    /// <summary>
    /// In-process implementation. With several API instances an instance only learns about a status change
    /// made elsewhere when its entry expires, so <see cref="CacheDuration"/> is the worst-case delay.
    /// </summary>
    public class MemoryUserActivityCache : IUserActivityCache
    {
        public static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);

        private readonly IMemoryCache _cache;

        public MemoryUserActivityCache(IMemoryCache cache)
        {
            _cache = cache;
        }

        public bool TryGetIsActive(Guid userId, out bool isActive)
        {
            return _cache.TryGetValue(Key(userId), out isActive);
        }

        public void SetIsActive(Guid userId, bool isActive)
        {
            _cache.Set(Key(userId), isActive, CacheDuration);
        }

        public void Invalidate(Guid userId)
        {
            _cache.Remove(Key(userId));
        }

        private static string Key(Guid userId) => $"UserActive:{userId}";
    }
}
