using System.Net;
using System.Net.Http.Json;
using ChatRoomNET.Web.UI.Blazor.Contracts;

namespace ChatRoomNET.Web.UI.Blazor.Services;

// Типизированная обёртка над REST API backend. HttpClient настроен в Program.cs
// (BaseAddress + BearerTokenHandler).
public class ChatApiClient(HttpClient http)
{
    public async Task<AuthResult> RegisterAsync(RegisterRequest request)
    {
        var response = await http.PostAsJsonAsync("api/auth/register", request);
        if (response.IsSuccessStatusCode)
        {
            return new AuthResult(await response.Content.ReadFromJsonAsync<AuthResponse>(), null);
        }

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>();
        var error = problem?.Errors is { Count: > 0 } errors
            ? string.Join(" ", errors.Values.SelectMany(messages => messages))
            : "Не удалось зарегистрироваться.";
        return new AuthResult(null, error);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request)
    {
        var response = await http.PostAsJsonAsync("api/auth/login", request);
        return response.IsSuccessStatusCode
            ? new AuthResult(await response.Content.ReadFromJsonAsync<AuthResponse>(), null)
            : new AuthResult(null, "Неверное имя пользователя или пароль.");
    }

    public async Task<IReadOnlyList<RoomResponse>> GetRoomsAsync() =>
        await http.GetFromJsonAsync<IReadOnlyList<RoomResponse>>("api/rooms") ?? [];

    // Публичные комнаты, в которые пользователь ещё не вступил.
    public async Task<IReadOnlyList<RoomResponse>> GetPublicRoomsAsync() =>
        await http.GetFromJsonAsync<IReadOnlyList<RoomResponse>>("api/rooms/public") ?? [];

    public async Task<RoomResponse?> CreateRoomAsync(CreateRoomRequest request)
    {
        var response = await http.PostAsJsonAsync("api/rooms", request);
        return response.IsSuccessStatusCode
            ? await response.Content.ReadFromJsonAsync<RoomResponse>()
            : null;
    }

    // null, если комната не найдена или инвайт-код неверный (404).
    public async Task<RoomResponse?> JoinRoomAsync(JoinRoomRequest request)
    {
        var response = await http.PostAsJsonAsync("api/rooms/join", request);
        return response.StatusCode == HttpStatusCode.NotFound
            ? null
            : await response.Content.ReadFromJsonAsync<RoomResponse>();
    }

    public async Task<IReadOnlyList<RoomMemberResponse>?> GetMembersAsync(Guid roomId)
    {
        var response = await http.GetAsync($"api/rooms/{roomId}/members");
        return response.StatusCode == HttpStatusCode.NotFound
            ? null
            : await response.Content.ReadFromJsonAsync<IReadOnlyList<RoomMemberResponse>>();
    }

    // Keyset-пагинация: before — курсор (Id самого старого показанного сообщения).
    public async Task<IReadOnlyList<MessageResponse>?> GetMessagesAsync(
        Guid roomId, long? before = null, int? take = null)
    {
        var query = new List<string>();
        if (before is not null) query.Add($"before={before}");
        if (take is not null) query.Add($"take={take}");
        var url = $"api/rooms/{roomId}/messages{(query.Count > 0 ? "?" + string.Join('&', query) : "")}";

        var response = await http.GetAsync(url);
        return response.StatusCode == HttpStatusCode.NotFound
            ? null
            : await response.Content.ReadFromJsonAsync<IReadOnlyList<MessageResponse>>();
    }
}
