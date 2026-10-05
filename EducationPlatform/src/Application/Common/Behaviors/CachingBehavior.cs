using System.Collections.Concurrent;
using System.Text.Json;
using Application.Interface;
using Application.Options;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Application.Common.Behaviors
{
    /// <summary>
    /// Marks a query whose answer may be reused for a short while. Meant for expensive, read-only reports
    /// (dashboards) where data that is a couple of minutes old is perfectly fine.
    /// </summary>
    public interface ICachedQuery
    {
        TimeSpan CacheDuration => TimeSpan.FromMinutes(2);
    }

    /// <summary>
    /// Serves <see cref="ICachedQuery"/> requests from memory. The key contains the request's parameters and the
    /// caller, so two users never share an answer unless they asked the same thing and are the same person.
    /// While one request computes a missing value, identical requests wait for it instead of hitting the
    /// database in parallel (dashboards are refreshed by many people at the same moment).
    /// </summary>
    public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new();

        private readonly IMemoryCache _cache;
        private readonly ICurrentUser _currentUser;
        private readonly CachingOptions _options;

        public CachingBehavior(IMemoryCache cache, ICurrentUser currentUser, IOptions<CachingOptions> options)
        {
            _cache = cache;
            _currentUser = currentUser;
            _options = options.Value;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            if (!_options.Enabled || request is not ICachedQuery cached)
                return await next();

            var key = $"query:{typeof(TRequest).FullName}:{_currentUser.Id}:{_currentUser.Role}:{JsonSerializer.Serialize(request, request.GetType())}";

            if (_cache.TryGetValue(key, out TResponse? hit) && hit is not null)
                return hit;

            var gate = Locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken);
            try
            {
                // Somebody may have filled it while we waited
                if (_cache.TryGetValue(key, out hit) && hit is not null)
                    return hit;

                var response = await next();
                _cache.Set(key, response, cached.CacheDuration);
                return response;
            }
            finally
            {
                gate.Release();

                // Keep the lock table from growing with every distinct parameter combination
                if (gate.CurrentCount == 1 && Locks.TryRemove(key, out var removed) && !ReferenceEquals(removed, gate))
                    Locks.TryAdd(key, removed);
            }
        }
    }
}
