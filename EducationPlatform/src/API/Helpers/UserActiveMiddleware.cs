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
                    status = new UserStatus(user != null && user.IsActive, user?.Role.ToString() ?? string.Empty);

                    activityCache.Set(userId, status);
                }

                if (!status.IsActive)
                {
                    await WriteProblemAsync(context, HttpStatusCode.Forbidden, "Forbidden",
                        "Your account is inactive or has been locked.");
                    return;
                }

                // A role change only reaches a token when it is reissued, so make the client do that now
                if (!string.Equals(status.Role, currentUser.Role, StringComparison.Ordinal))
                {
                    await WriteProblemAsync(context, HttpStatusCode.Unauthorized, "Unauthorized",
                        "Your permissions have changed. Please refresh your session.");
                    return;
                }
            }

            await _next(context);
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
