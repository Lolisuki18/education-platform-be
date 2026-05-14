using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace API.Hubs
{
    public class AuthHub : Hub
    {
        #region Attributes
        private static readonly ConcurrentDictionary<string, HashSet<string>> _connections = new();
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
                var connections = _connections.GetOrAdd(userId, _ => new HashSet<string>());
                lock (connections)
                {
                    connections.Add(Context.ConnectionId);
                }
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?
                .FindFirst(ClaimTypes.NameIdentifier)
                ?.Value;

            if (!string.IsNullOrEmpty(userId) &&
                _connections.TryGetValue(userId, out var connections))
            {
                lock (connections)
                {
                    connections.Remove(Context.ConnectionId);
                    if (connections.Count == 0)
                        _connections.TryRemove(userId, out _);
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        public static async Task ForceLogout(
            IHubContext<AuthHub> hubContext,
            string userId)
        {
            if (_connections.TryGetValue(userId, out var connections))
            {
                await hubContext.Clients
                    .Clients(connections.ToList())
                    .SendAsync("ForceLogout");
            }
        }
        #endregion
    }
}
