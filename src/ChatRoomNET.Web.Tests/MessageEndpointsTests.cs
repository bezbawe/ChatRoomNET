using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChatRoomNET.Web.Contracts;
using ChatRoomNET.Web.Data;
using ChatRoomNET.Web.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ChatRoomNET.Web.Tests;

public class MessageEndpointsTests : IDisposable
{
    private readonly AuthWebApplicationFactory _factory = new();
    private readonly List<HttpClient> _clients = [];

    private async Task<(HttpClient Client, string UserId)> CreateAuthenticatedClientAsync(string userName)
    {
        var client = _factory.CreateClient();
        _clients.Add(client);

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(userName, $"{userName}@example.com", "Passw0rd!"));
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return (client, auth.UserId);
    }

    private static async Task<RoomResponse> CreateRoomAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/rooms", new CreateRoomRequest("General", false));
        return (await response.Content.ReadFromJsonAsync<RoomResponse>())!;
    }

    private async Task SeedMessagesAsync(Guid roomId, string userId, int count)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();

        var baseTime = DateTimeOffset.UtcNow;
        for (var i = 0; i < count; i++)
        {
            db.Messages.Add(new Message
            {
                RoomId = roomId,
                UserId = userId,
                Text = $"msg {i}",
                CreatedAt = baseTime.AddSeconds(i)
            });
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Messages_ReturnsNewestPage_InAscendingOrder()
    {
        var (client, userId) = await CreateAuthenticatedClientAsync("alice");
        var room = await CreateRoomAsync(client);
        await SeedMessagesAsync(room.Id, userId, 5);

        var page = await client.GetFromJsonAsync<MessageResponse[]>($"/api/rooms/{room.Id}/messages?take=3");

        Assert.NotNull(page);
        Assert.Equal(3, page!.Length);
        Assert.Equal(["msg 2", "msg 3", "msg 4"], page.Select(m => m.Text));
        Assert.True(page[0].Id < page[1].Id && page[1].Id < page[2].Id);
    }

    [Fact]
    public async Task Messages_WithBeforeCursor_ReturnsOlderPage()
    {
        var (client, userId) = await CreateAuthenticatedClientAsync("alice");
        var room = await CreateRoomAsync(client);
        await SeedMessagesAsync(room.Id, userId, 5);

        var firstPage = await client.GetFromJsonAsync<MessageResponse[]>($"/api/rooms/{room.Id}/messages?take=3");
        var cursor = firstPage![0].Id;

        var olderPage = await client.GetFromJsonAsync<MessageResponse[]>(
            $"/api/rooms/{room.Id}/messages?before={cursor}&take=3");

        Assert.NotNull(olderPage);
        Assert.Equal(["msg 0", "msg 1"], olderPage!.Select(m => m.Text));
        Assert.True(olderPage[^1].Id < cursor);
    }

    [Fact]
    public async Task Messages_DefaultTake_Caps30()
    {
        var (client, userId) = await CreateAuthenticatedClientAsync("alice");
        var room = await CreateRoomAsync(client);
        await SeedMessagesAsync(room.Id, userId, 35);

        var page = await client.GetFromJsonAsync<MessageResponse[]>($"/api/rooms/{room.Id}/messages");

        Assert.NotNull(page);
        Assert.Equal(30, page!.Length);
        Assert.Equal("msg 34", page[^1].Text);
    }

    [Fact]
    public async Task Messages_IncludesAuthorUserName()
    {
        var (client, userId) = await CreateAuthenticatedClientAsync("alice");
        var room = await CreateRoomAsync(client);
        await SeedMessagesAsync(room.Id, userId, 1);

        var page = await client.GetFromJsonAsync<MessageResponse[]>($"/api/rooms/{room.Id}/messages");

        Assert.Equal("alice", page![0].UserName);
    }

    [Fact]
    public async Task Messages_AsNonMember_ReturnsNotFound()
    {
        var (alice, aliceId) = await CreateAuthenticatedClientAsync("alice");
        var (bob, _) = await CreateAuthenticatedClientAsync("bob");
        var room = await CreateRoomAsync(alice);
        await SeedMessagesAsync(room.Id, aliceId, 3);

        var response = await bob.GetAsync($"/api/rooms/{room.Id}/messages");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Messages_WithoutToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        _clients.Add(client);

        var response = await client.GetAsync($"/api/rooms/{Guid.NewGuid()}/messages");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public void Dispose()
    {
        foreach (var client in _clients)
        {
            client.Dispose();
        }

        _factory.Dispose();
    }
}
