using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace OrderService.Api.Hubs
{
    // Clients only receive pushes; they call no hub methods.
    [Authorize]
    public class NotificationHub : Hub
    {
        public const string AdminGroup = "admins";

        public static string UserGroup(int userId) => $"user-{userId}";

        public override async Task OnConnectedAsync()
        {
            var user = Context.User;

            if (int.TryParse(user?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
            }

            if (user?.IsInRole("Admin") == true)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup);
            }

            await base.OnConnectedAsync();
        }
    }
}
