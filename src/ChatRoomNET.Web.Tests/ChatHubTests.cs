using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChatRoomNET.Web.Contracts;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace ChatRoomNET.Web.Tests;

public class ChatHubTests : IAsyncLifetime
{
    private readonly AuthWebApplicationFactory _factory = new();
    private readonly List<HttpClient> _clients = [];
    private readonly List<HubConnection> _connections = [];

    private async Task<AuthResponse> RegisterAsync(string userName)
    {
        var client = _factory.CreateClient();
        _clients.Add(client);

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(userName, $"{userName}@example.com", "Passw0rd!"));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    private async Task<RoomResponse> CreateRoomAsync(AuthResponse owner)
    {
        var client = _factory.CreateClient();
        _clients.Add(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", owner.Token);

        var response = await client.PostAsJsonAsync("/api/rooms", new CreateRoomRequest("General", false));
        return (await response.Content.ReadFromJsonAsync<RoomResponse>())!;
    }

    private async Task JoinRoomViaApiAsync(AuthResponse user, string inviteCode)
    {
        var client = _factory.CreateClient();
        _clients.Add(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);

        await client.PostAsJsonAsync("/api/rooms/join", new JoinRoomRequest(null, inviteCode));
    }

    // TestServer не поддерживает WebSocket в этих тестах — форсим LongPolling через его HTTP-handler.
    // Токен уходит Bearer-заголовком (в рантайме WebSocket положит его в query string — см. Program.cs).
    private HubConnection CreateHubConnection(string? token)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/chat", options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                if (token is not null)
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                }
            })
            .Build();
        _connections.Add(connection);
        return connection;
    }

    private static async Task<T> WaitForAsync<T>(TaskCompletionSource<T> tcs)
    {
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.True(completed == tcs.Task, "Ожидаемое событие хаба не пришло за отведённое время.");
        return await tcs.Task;
    }

    [Fact]
    public async Task Connect_WithoutToken_Fails()
    {
        var connection = CreateHubConnection(token: null);

        await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
    }

    [Fact]
    public async Task Connect_WithToken_Succeeds()
    {
        var alice = await RegisterAsync("alice");
        var connection = CreateHubConnection(alice.Token);

        await connection.StartAsync();

        Assert.Equal(HubConnectionState.Connected, connection.State);
    }

    [Fact]
    public async Task SendMessage_BroadcastsToRoomMembers()
    {
        var alice = await RegisterAsync("alice");
        var bob = await RegisterAsync("bob");
        var room = await CreateRoomAsync(alice);
        await JoinRoomViaApiAsync(bob, room.InviteCode!);

        var received = new TaskCompletionSource<MessageResponse>();

        var aliceHub = CreateHubConnection(alice.Token);
        var bobHub = CreateHubConnection(bob.Token);
        bobHub.On<MessageResponse>("ReceiveMessage", m => received.TrySetResult(m));

        await aliceHub.StartAsync();
        await bobHub.StartAsync();
        await aliceHub.InvokeAsync("JoinRoom", room.Id);
        await bobHub.InvokeAsync("JoinRoom", room.Id);

        await aliceHub.InvokeAsync("SendMessage", room.Id, "hello room");

        var message = await WaitForAsync(received);
        Assert.Equal("hello room", message.Text);
        Assert.Equal("alice", message.UserName);
        Assert.Equal(room.Id, message.RoomId);
    }

    [Fact]
    public async Task Typing_NotifiesOthersInRoom()
    {
        var alice = await RegisterAsync("alice");
        var bob = await RegisterAsync("bob");
        var room = await CreateRoomAsync(alice);
        await JoinRoomViaApiAsync(bob, room.InviteCode!);

        var typing = new TaskCompletionSource<UserTypingNotification>();

        var aliceHub = CreateHubConnection(alice.Token);
        var bobHub = CreateHubConnection(bob.Token);
        bobHub.On<UserTypingNotification>("UserTyping", n => typing.TrySetResult(n));

        await aliceHub.StartAsync();
        await bobHub.StartAsync();
        await aliceHub.InvokeAsync("JoinRoom", room.Id);
        await bobHub.InvokeAsync("JoinRoom", room.Id);

        await aliceHub.InvokeAsync("Typing", room.Id);

        var notification = await WaitForAsync(typing);
        Assert.Equal("alice", notification.UserName);
        Assert.Equal(room.Id, notification.RoomId);
    }

    [Fact]
    public async Task Presence_BroadcastsOnConnectAndDisconnect()
    {
        var alice = await RegisterAsync("alice");
        var bob = await RegisterAsync("bob");

        var online = new TaskCompletionSource<PresenceNotification>();
        var offline = new TaskCompletionSource<PresenceNotification>();

        // bob подключается первым и слушает статусы.
        var bobHub = CreateHubConnection(bob.Token);
        bobHub.On<PresenceNotification>("PresenceChanged", n =>
        {
            if (n.UserId == alice.UserId && n.IsOnline)
            {
                online.TrySetResult(n);
            }
            else if (n.UserId == alice.UserId && !n.IsOnline)
            {
                offline.TrySetResult(n);
            }
        });
        await bobHub.StartAsync();

        var aliceHub = CreateHubConnection(alice.Token);
        await aliceHub.StartAsync();

        var onlineNotification = await WaitForAsync(online);
        Assert.Equal("alice", onlineNotification.UserName);

        await aliceHub.StopAsync();

        var offlineNotification = await WaitForAsync(offline);
        Assert.False(offlineNotification.IsOnline);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var connection in _connections)
        {
            await connection.DisposeAsync();
        }

        foreach (var client in _clients)
        {
            client.Dispose();
        }

        _factory.Dispose();
    }
}
