using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChatRoomNET.Web.Contracts;

namespace ChatRoomNET.Web.Tests;

public class RoomEndpointsTests : IDisposable
{
    private readonly AuthWebApplicationFactory _factory = new();
    private readonly List<HttpClient> _clients = [];

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string userName)
    {
        var client = _factory.CreateClient();
        _clients.Add(client);

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(userName, $"{userName}@example.com", "Passw0rd!"));
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    [Fact]
    public async Task Create_ReturnsCreatedRoomWithInviteCode()
    {
        var client = await CreateAuthenticatedClientAsync("alice");

        var response = await client.PostAsJsonAsync("/api/rooms", new CreateRoomRequest("General", false));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var room = await response.Content.ReadFromJsonAsync<RoomResponse>();
        Assert.NotNull(room);
        Assert.Equal("General", room!.Name);
        Assert.False(string.IsNullOrWhiteSpace(room.InviteCode));
    }

    [Fact]
    public async Task GetMyRooms_ReturnsOnlyRoomsUserIsMemberOf()
    {
        var alice = await CreateAuthenticatedClientAsync("alice");
        var bob = await CreateAuthenticatedClientAsync("bob");

        await alice.PostAsJsonAsync("/api/rooms", new CreateRoomRequest("Alice's room", false));
        await bob.PostAsJsonAsync("/api/rooms", new CreateRoomRequest("Bob's room", false));

        var rooms = await alice.GetFromJsonAsync<RoomResponse[]>("/api/rooms");

        Assert.NotNull(rooms);
        Assert.Single(rooms!);
        Assert.Equal("Alice's room", rooms![0].Name);
    }

    [Fact]
    public async Task Join_WithInviteCode_AddsMembership()
    {
        var alice = await CreateAuthenticatedClientAsync("alice");
        var bob = await CreateAuthenticatedClientAsync("bob");

        var created = await alice.PostAsJsonAsync("/api/rooms", new CreateRoomRequest("Private", true));
        var room = await created.Content.ReadFromJsonAsync<RoomResponse>();

        var join = await bob.PostAsJsonAsync("/api/rooms/join", new JoinRoomRequest(null, room!.InviteCode));

        Assert.Equal(HttpStatusCode.OK, join.StatusCode);
        var bobRooms = await bob.GetFromJsonAsync<RoomResponse[]>("/api/rooms");
        Assert.Contains(bobRooms!, r => r.Id == room.Id);
    }

    [Fact]
    public async Task Join_WithUnknownCode_ReturnsNotFound()
    {
        var client = await CreateAuthenticatedClientAsync("alice");

        var response = await client.PostAsJsonAsync("/api/rooms/join", new JoinRoomRequest(null, "NOPE1234"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Join_PublicRoomById_Succeeds()
    {
        var alice = await CreateAuthenticatedClientAsync("alice");
        var bob = await CreateAuthenticatedClientAsync("bob");

        var created = await alice.PostAsJsonAsync("/api/rooms", new CreateRoomRequest("Public", false));
        var room = await created.Content.ReadFromJsonAsync<RoomResponse>();

        var response = await bob.PostAsJsonAsync("/api/rooms/join", new JoinRoomRequest(room!.Id, null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Join_PrivateRoomById_ReturnsNotFound()
    {
        var alice = await CreateAuthenticatedClientAsync("alice");
        var bob = await CreateAuthenticatedClientAsync("bob");

        var created = await alice.PostAsJsonAsync("/api/rooms", new CreateRoomRequest("Private", true));
        var room = await created.Content.ReadFromJsonAsync<RoomResponse>();

        var response = await bob.PostAsJsonAsync("/api/rooms/join", new JoinRoomRequest(room!.Id, null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Members_AsMember_ReturnsParticipants()
    {
        var alice = await CreateAuthenticatedClientAsync("alice");
        var bob = await CreateAuthenticatedClientAsync("bob");

        var created = await alice.PostAsJsonAsync("/api/rooms", new CreateRoomRequest("General", false));
        var room = await created.Content.ReadFromJsonAsync<RoomResponse>();
        await bob.PostAsJsonAsync("/api/rooms/join", new JoinRoomRequest(null, room!.InviteCode));

        var members = await alice.GetFromJsonAsync<RoomMemberResponse[]>($"/api/rooms/{room.Id}/members");

        Assert.NotNull(members);
        Assert.Equal(2, members!.Length);
        Assert.Contains(members, m => m.UserName == "alice");
        Assert.Contains(members, m => m.UserName == "bob");
    }

    [Fact]
    public async Task Members_AsNonMember_ReturnsNotFound()
    {
        var alice = await CreateAuthenticatedClientAsync("alice");
        var bob = await CreateAuthenticatedClientAsync("bob");

        var created = await alice.PostAsJsonAsync("/api/rooms", new CreateRoomRequest("Private", true));
        var room = await created.Content.ReadFromJsonAsync<RoomResponse>();

        var response = await bob.GetAsync($"/api/rooms/{room!.Id}/members");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Rooms_WithoutToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        _clients.Add(client);

        var response = await client.GetAsync("/api/rooms");

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
