using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace API.Hubs
{
    [Authorize]
    public class CourseHub : Hub
    {
        /// <summary>Every connection of one user (phone, laptop, ...) joins this group, so a notification reaches all of them.</summary>
        public static string UserGroup(Guid userId) => $"user-{userId}";

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (Guid.TryParse(userId, out var id))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(id));
            }

            await base.OnConnectedAsync();
        }
    }
}
