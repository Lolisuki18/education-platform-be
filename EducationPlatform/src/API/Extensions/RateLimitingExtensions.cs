using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;

namespace API.Extensions
{
    public static class RateLimitPolicies
    {
        public const string Login = "login";
        public const string Register = "register";
        public const string VerifyEmail = "verify-email";
        public const string RefreshToken = "refresh-token";
        public const string Upload = "upload";
        public const string Account = "account";
    }

    public class RateLimitingOptions
    {
        public bool Enabled { get; set; } = true;
    }

    public static class RateLimitingExtensions
    {
        /// <summary>
        /// Every policy is partitioned per client (per user once signed in, per IP otherwise), so one noisy client
        /// cannot use up the budget of everybody else. Set <c>RateLimiting:Enabled</c> to false to switch it all off.
        /// </summary>
        public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
        {
            // Read per request, not here: configuration added later (tests, hosting overrides) must still count
            services.AddOptions<RateLimitingOptions>()
                .Bind(configuration.GetSection("RateLimiting"));

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.OnRejected = (context, _) =>
                {
                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    {
                        context.HttpContext.Response.Headers.RetryAfter =
                            ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                    }

                    return ValueTask.CompletedTask;
                };

                // A safety net for the whole API
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    Partition(IsEnabled(context), ClientKey(context), 300, TimeSpan.FromMinutes(1)));

                // Credentials and one-time codes: brute-force targets
                options.AddPolicy(RateLimitPolicies.Login, context =>
                    Partition(IsEnabled(context), ClientKey(context), 10, TimeSpan.FromMinutes(1)));

                options.AddPolicy(RateLimitPolicies.Register, context =>
                    Partition(IsEnabled(context), ClientKey(context), 5, TimeSpan.FromMinutes(10)));

                options.AddPolicy(RateLimitPolicies.VerifyEmail, context =>
                    Partition(IsEnabled(context), ClientKey(context), 10, TimeSpan.FromMinutes(5)));

                options.AddPolicy(RateLimitPolicies.RefreshToken, context =>
                    Partition(IsEnabled(context), ClientKey(context), 30, TimeSpan.FromMinutes(1)));

                // Re-entering the password to delete an account, and the personal data download
                options.AddPolicy(RateLimitPolicies.Account, context =>
                    Partition(IsEnabled(context), ClientKey(context), 5, TimeSpan.FromMinutes(10)));

                // A video is uploaded as many chunks, so this is generous per user but still bounded
                options.AddPolicy(RateLimitPolicies.Upload, context =>
                    Partition(IsEnabled(context), ClientKey(context), 600, TimeSpan.FromMinutes(1)));
            });

            return services;
        }

        private static bool IsEnabled(HttpContext context) =>
            context.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<RateLimitingOptions>>().Value.Enabled;

        private static RateLimitPartition<string> Partition(bool enabled, string key, int permitLimit, TimeSpan window)
        {
            if (!enabled)
                return RateLimitPartition.GetNoLimiter(key);

            return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueLimit = 0,
                AutoReplenishment = true
            });
        }

        private static string ClientKey(HttpContext context)
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
                return $"user:{userId}";

            return $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
        }

        /// <summary>
        /// Lets the app see the real client IP and scheme behind a reverse proxy / load balancer. Without it every
        /// client shares the proxy's address and therefore one rate-limit bucket. Only trusted proxies may set the
        /// headers: list them under <c>ForwardedHeaders:KnownProxies</c> / <c>KnownNetworks</c> (CIDR), or set
        /// <c>ForwardedHeaders:TrustAll</c> when the app is only reachable through the proxy (e.g. a private Docker network).
        /// </summary>
        public static IServiceCollection AddForwardedHeadersSupport(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

                foreach (var proxy in configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? Array.Empty<string>())
                {
                    options.KnownProxies.Add(IPAddress.Parse(proxy));
                }

                foreach (var network in configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? Array.Empty<string>())
                {
                    var parts = network.Split('/');
                    options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(
                        IPAddress.Parse(parts[0]),
                        int.Parse(parts[1], CultureInfo.InvariantCulture)));
                }

                if (configuration.GetValue("ForwardedHeaders:TrustAll", false))
                {
                    options.KnownNetworks.Clear();
                    options.KnownProxies.Clear();
                }
            });

            return services;
        }
    }
}
