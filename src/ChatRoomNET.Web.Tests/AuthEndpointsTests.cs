using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChatRoomNET.Web.Contracts;

namespace ChatRoomNET.Web.Tests;

public class AuthEndpointsTests : IDisposable
{
    private readonly AuthWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public AuthEndpointsTests()
    {
        _client = _factory.CreateClient();
    }

    private static RegisterRequest NewRegisterRequest(string userName = "alice") =>
        new(userName, $"{userName}@example.com", "Passw0rd!");

    [Fact]
    public async Task Register_WithValidData_ReturnsTokenAndUser()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", NewRegisterRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
        Assert.Equal("alice", body.UserName);
        Assert.False(string.IsNullOrWhiteSpace(body.UserId));
    }

    [Fact]
    public async Task Register_WithDuplicateUserName_ReturnsValidationProblem()
    {
        await _client.PostAsJsonAsync("/api/auth/register", NewRegisterRequest());

        var response = await _client.PostAsJsonAsync("/api/auth/register", NewRegisterRequest());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsToken()
    {
        await _client.PostAsJsonAsync("/api/auth/register", NewRegisterRequest());

        var response = await _client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("alice", "Passw0rd!"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        await _client.PostAsJsonAsync("/api/auth/register", NewRegisterRequest());

        var response = await _client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("alice", "WrongPassword!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownUser_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("ghost", "Passw0rd!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithInvalidToken_ReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "not-a-valid-jwt");

        var response = await _client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithValidToken_ReturnsAuthenticatedUser()
    {
        var register = await _client.PostAsJsonAsync("/api/auth/register", NewRegisterRequest());
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.Token);
        var response = await _client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<MeResponse>();
        Assert.Equal(auth.UserId, me!.Id);
        Assert.Equal("alice", me.UserName);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private record MeResponse(string Id, string UserName);
}
