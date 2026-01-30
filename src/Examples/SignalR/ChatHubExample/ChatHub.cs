using Microsoft.AspNetCore.SignalR;

namespace ChatHubExample;

/// <summary>
/// A simple chat hub that broadcasts messages to all connected clients.
/// </summary>
public sealed class ChatHub : Hub
{
    public async Task SendMessage(string user, string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }

    public async Task JoinGroup(string groupName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        await Clients.Group(groupName).SendAsync("ReceiveMessage", "System", $"A user joined {groupName}");
    }

    public async Task SendToGroup(string groupName, string user, string message)
    {
        await Clients.Group(groupName).SendAsync("ReceiveMessage", user, message);
    }

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("ReceiveMessage", "System", "Welcome to the chat!");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Clients.All.SendAsync("ReceiveMessage", "System", "A user has left the chat.");
        await base.OnDisconnectedAsync(exception);
    }
}
