using System.Net;
using Application.Interface;
using Infrastructure.Services;

namespace API.Middlewares
{
    /// <summary>
    /// Sits in front of the static-file middleware: everything under <c>/media/videos/</c> needs a valid
    /// expiry + signature in the query string, so paid lesson videos cannot be fetched by anyone who merely
    /// knows or guesses a file name. Other media (thumbnails, ...) stays public.
    /// </summary>
    public class ProtectedMediaMiddleware
    {
        private readonly RequestDelegate _next;

        public ProtectedMediaMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IMediaUrlSigner signer)
        {
            if (context.Request.Path.StartsWithSegments("/media", out var remaining)
                && HmacMediaUrlSigner.TryGetProtectedPath(remaining.Value ?? string.Empty, out var path))
            {
                var allowed = long.TryParse(context.Request.Query["exp"], out var expires)
                              && signer.IsValid(path, expires, context.Request.Query["sig"].ToString());

                if (!allowed)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    await context.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
                    {
                        Status = (int)HttpStatusCode.Forbidden,
                        Title = "Forbidden",
                        Detail = "This video link is missing, invalid or has expired. Open the lesson again to get a new one."
                    });
                    return;
                }

                // A signed URL is a bearer credential: browsers and proxies must not keep a shared copy
                context.Response.Headers.CacheControl = "private, no-store";
            }

            await _next(context);
        }
    }
}
