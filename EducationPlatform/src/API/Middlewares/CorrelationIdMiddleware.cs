using System.Text.RegularExpressions;
using Serilog.Context;

namespace API.Middlewares
{
    public partial class CorrelationIdMiddleware
    {
        public const string HeaderName = "X-Correlation-Id";

        private readonly RequestDelegate _next;

        public CorrelationIdMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var correlationId = Resolve(
                context.Request.Headers.TryGetValue(HeaderName, out var existing) ? existing.ToString() : null,
                context.TraceIdentifier);

            context.Response.Headers[HeaderName] = correlationId;

            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                await _next(context);
            }
        }

        /// <summary>
        /// A caller may supply its own id to trace a request across services, but the value ends up in every log
        /// line and in a response header, so only short, plain identifiers are accepted. Anything else (line
        /// breaks that forge log entries, huge values, odd characters) is replaced by the server-generated id.
        /// </summary>
        public static string Resolve(string? supplied, string fallback)
        {
            return !string.IsNullOrWhiteSpace(supplied) && SafeId().IsMatch(supplied)
                ? supplied
                : fallback;
        }

        [GeneratedRegex(@"^[A-Za-z0-9._:\-]{1,64}$")]
        private static partial Regex SafeId();
    }
}
