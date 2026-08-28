using ChatRoomNET.Web.Contracts;
using ChatRoomNET.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ChatRoomNET.Web.Hubs;

[Authorize]
public class ChatHub(IMessageService messages, IRoomService rooms, IPresenceTracker presence) : Hub
{
    // Клиент подписывается на комнату (SignalR-группу). В группу пускаем только участников,
    // иначе не-участник получал бы чужие сообщения через broadcast.
    public async Task JoinRoom(Guid roomId)
    {
        if (!await rooms.IsMemberAsync(roomId, UserId))
        {
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(roomId));
    }

    public Task LeaveRoom(Guid roomId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(roomId));

    public async Task SendMessage(Guid roomId, string text)
    {
        var message = await messages.SendAsync(roomId, UserId, text);
        if (message is null)
        {
            return;
        }

        await Clients.Group(GroupName(roomId)).SendAsync("ReceiveMessage", message);
    }

    public Task Typing(Guid roomId) =>
        Clients.OthersInGroup(GroupName(roomId))
            .SendAsync("UserTyping", new UserTypingNotification(roomId, UserId, UserName));

    public override async Task OnConnectedAsync()
    {
        if (presence.Connect(UserId, Context.ConnectionId))
        {
            await Clients.All.SendAsync("PresenceChanged", new PresenceNotification(UserId, UserName, true));
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (presence.Disconnect(UserId, Context.ConnectionId))
        {
            await Clients.All.SendAsync("PresenceChanged", new PresenceNotification(UserId, UserName, false));
        }

        await base.OnDisconnectedAsync(exception);
    }

    private string UserId => Context.UserIdentifier!;
    private string UserName => Context.User!.Identity!.Name!;
    private static string GroupName(Guid roomId) => $"room:{roomId}";
}
