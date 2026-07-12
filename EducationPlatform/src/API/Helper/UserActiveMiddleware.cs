using System;
using System.Net;
using System.Threading.Tasks;
using Application.Interface;
using Domain.Common.Interfaces;
using Domain.IdentityManagement.Aggregate;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;

namespace API.Helper
{
    public class UserActiveMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IMemoryCache _memoryCache;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);

        public UserActiveMiddleware(RequestDelegate next, IMemoryCache memoryCache)
        {
            _next = next;
            _memoryCache = memoryCache;
        }

        public async Task InvokeAsync(HttpContext context, IUnitOfWork unitOfWork, ICurrentUser currentUser)
        {
            if (currentUser.IsAuthenticated && currentUser.Id.HasValue)
            {
                var userId = currentUser.Id.Value;
                var cacheKey = $"UserActive:{userId}";

                if (!_memoryCache.TryGetValue(cacheKey, out bool isActive))
                {
                    var user = await unitOfWork.GetRepository<IUserRepository>().GetByIdAsync(userId);
                    isActive = user != null && user.IsActive;

                    _memoryCache.Set(cacheKey, isActive, CacheDuration);
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
