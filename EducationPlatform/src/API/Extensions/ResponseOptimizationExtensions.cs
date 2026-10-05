using System.IO.Compression;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.ResponseCompression;

namespace API.Extensions
{
    public static class OutputCachePolicies
    {
        /// <summary>Public, read-only listings that every visitor sees the same way.</summary>
        public const string PublicListing = "PublicListing";
    }

    /// <summary>
    /// Caches a response for a few seconds, but only for visitors who are not signed in. Signed-in users (admins,
    /// teachers, students) get different data from the same URL, and their requests must never be served from or
    /// stored in a cache that other people read.
    /// </summary>
    public class AnonymousOnlyCachePolicy : IOutputCachePolicy
    {

        public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellation)
        {
            var request = context.HttpContext.Request;

            // Anything that could identify a user (the JWT travels in a header or, for browsers, in a cookie)
            var anonymous = context.HttpContext.User.Identity?.IsAuthenticated != true
                            && !request.Headers.ContainsKey("Authorization")
                            && !request.Cookies.ContainsKey("access_token");

            // Read per request: configuration added after start-up (tests, hosting overrides) must still count
            var settings = context.HttpContext.RequestServices
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<OutputCacheSettings>>().Value;

            var cacheable = settings.Enabled && anonymous && HttpMethods.IsGet(request.Method);

            context.EnableOutputCaching = cacheable;
            context.AllowCacheLookup = cacheable;
            context.AllowCacheStorage = cacheable;
            context.AllowLocking = true;
            context.ResponseExpirationTimeSpan = TimeSpan.FromSeconds(Math.Max(1, settings.PublicSeconds));

            // The same URL may be called from different origins and with different filters
            context.CacheVaryByRules.QueryKeys = "*";
            context.CacheVaryByRules.HeaderNames = "Origin";

            return ValueTask.CompletedTask;
        }

        public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellation)
            => ValueTask.CompletedTask;

        public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellation)
        {
            var response = context.HttpContext.Response;

            // Only successful answers without per-visitor state are worth keeping
            if (response.StatusCode != StatusCodes.Status200OK || response.Headers.ContainsKey("Set-Cookie"))
            {
                context.AllowCacheStorage = false;
            }

            return ValueTask.CompletedTask;
        }
    }

    public class OutputCacheSettings
    {
        public bool Enabled { get; set; } = true;

        /// <summary>How long a public listing is reused.</summary>
        public int PublicSeconds { get; set; } = 30;
    }

    public static class ResponseOptimizationExtensions
    {
        public static IServiceCollection AddResponseOptimizations(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.Providers.Add<BrotliCompressionProvider>();
                options.Providers.Add<GzipCompressionProvider>();
                options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[] { "application/problem+json" });
            });
            services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
            services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

            services.AddOptions<OutputCacheSettings>()
                .Bind(configuration.GetSection("OutputCache"));

            services.AddOutputCache(options =>
            {
                options.AddPolicy(OutputCachePolicies.PublicListing, new AnonymousOnlyCachePolicy());
            });

            return services;
        }
    }
}
