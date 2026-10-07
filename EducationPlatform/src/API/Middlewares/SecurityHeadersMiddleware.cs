namespace API.Middlewares
{
    /// <summary>
    /// Standard hardening headers on every response. They cost nothing and close off whole classes of browser
    /// attacks (content sniffing, framing, leaking the URL in the Referer header).
    /// </summary>
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public Task InvokeAsync(HttpContext context)
        {
            var headers = context.Response.Headers;

            // A response is only ever what its Content-Type says: an uploaded file cannot be re-read as a script
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

            // The API only answers with data. Swagger UI is served from the root and needs its own scripts and
            // styles, so the strict policy applies to the API routes only.
            if (context.Request.Path.StartsWithSegments("/api"))
                headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";

            return _next(context);
        }
    }
}
