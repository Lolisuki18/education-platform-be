using System.Net;
using Application.Interface;
using Domain.Common.Interfaces;
using Domain.IdentityManagement.Aggregate;

namespace API.Helpers
{
    /// <summary>Rejects requests from accounts an admin has deactivated, even if their access token is still valid.</summary>
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

                if (!activityCache.TryGetIsActive(userId, out var isActive))
                {
                    var user = await unitOfWork.GetRepository<IUserRepository>().GetByIdAsync(userId);
                    isActive = user != null && user.IsActive;

                    activityCache.SetIsActive(userId, isActive);
                }

                if (!isActive)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
                    {
                        Status = (int)HttpStatusCode.Forbidden,
                        Title = "Forbidden",
                        Detail = "Your account is inactive or has been locked.",
                        Instance = $"{context.Request.Method} {context.Request.Path}"
                    });
                    return;
                }
            }

            await _next(context);
        }
    }
}
