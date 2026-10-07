using System.Net;
using Application.Interface;
using Domain.Common.Interfaces;
using Domain.IdentityManagement.Aggregate;

namespace API.Helpers
{
    /// <summary>
    /// Rejects requests from accounts an admin has deactivated, and from tokens that carry an outdated role,
    /// even though the access token itself is still valid.
    /// </summary>
    public class UserActiveMiddleware
    {
        private readonly RequestDelegate _next;

        public UserActiveMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(
            HttpContext context,
            IUnitOfWork unitOfWork,
            ICurrentUser currentUser,
            IUserActivityCache activityCache)
        {
            if (currentUser.IsAuthenticated && currentUser.Id.HasValue)
            {
                var userId = currentUser.Id.Value;

                if (!activityCache.TryGet(userId, out var status))
                {
                    var user = await unitOfWork.GetRepository<IUserRepository>().GetByIdAsync(userId);
                    status = new UserStatus(user != null && user.IsActive, user?.Role.ToString() ?? string.Empty, user?.TokensValidFrom);

                    activityCache.Set(userId, status);
                }

                if (!status.IsActive)
                {
                    await WriteProblemAsync(context, HttpStatusCode.Forbidden, "Forbidden",
                        "Your account is inactive or has been locked.");
                    return;
                }

                var allowsStaleRole = context.GetEndpoint()?.Metadata.GetMetadata<AllowStaleRoleAttribute>() != null;

                // Every session was ended (password changed or reset, "log out everywhere", account deleted) after this
                // token was issued. The endpoints that renew or end a session are exempt, as for the role below.
                if (!allowsStaleRole && IsIssuedBefore(context, status.TokensValidFrom))
                {
                    await WriteProblemAsync(context, HttpStatusCode.Unauthorized, "Unauthorized",
                        "Your session has ended. Please sign in again.");
                    return;
                }

                // A role change only reaches a token when it is reissued, so make the client do that now
                // (except on the endpoints that do the reissuing, or the client could never recover)
                if (!allowsStaleRole && !string.Equals(status.Role, currentUser.Role, StringComparison.Ordinal))
                {
                    await WriteProblemAsync(context, HttpStatusCode.Unauthorized, "Unauthorized",
                        "Your permissions have changed. Please refresh your session.");
                    return;
                }
            }

            await _next(context);
        }

        /// <summary>
        /// True when the token's <c>iat</c> (whole seconds) is earlier than the moment all sessions were ended. A token
        /// without <c>iat</c> predates the check, so it counts as issued before. Comparing whole seconds means a
        /// token issued within the same second as the change is still accepted, which is what the fresh token of
        /// a password change needs.
        /// </summary>
        public static bool IsIssuedBefore(HttpContext context, DateTime? validFrom)
        {
            if (validFrom == null)
                return false;

            var issuedAt = context.User.FindFirst("iat")?.Value;
            if (!long.TryParse(issuedAt, out var issuedAtSeconds))
                return true;

            var validFromSeconds = new DateTimeOffset(DateTime.SpecifyKind(validFrom.Value, DateTimeKind.Utc)).ToUnixTimeSeconds();
            return issuedAtSeconds < validFromSeconds;
        }

        private static Task WriteProblemAsync(HttpContext context, HttpStatusCode status, string title, string detail)
        {
            context.Response.StatusCode = (int)status;
            context.Response.ContentType = "application/json";
            return context.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = (int)status,
                Title = title,
                Detail = detail,
                Instance = $"{context.Request.Method} {context.Request.Path}"
            });
        }
    }
}
