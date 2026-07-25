using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace API.Hubs
{
    [Authorize]
    public class AuthHub : Hub
    {
        #region Attributes
        #endregion

        #region Properties
        #endregion

        #region Methods
        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?
                .FindFirst(ClaimTypes.NameIdentifier)
                ?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
        }

        public static async Task ForceLogout(
            IHubContext<AuthHub> hubContext,
            string userId)
        {
            await hubContext.Clients
                .Group($"user-{userId}")
                .SendAsync("ForceLogout");
        }
        #endregion
    }
}
