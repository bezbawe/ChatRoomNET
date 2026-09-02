namespace ChatRoomNET.Web.UI.Blazor.Contracts;

// Клиентские копии контрактов backend (WASM не ссылается на ChatRoomNET.Web).
// Держи в синхроне с ChatRoomNET.Web/Contracts.

public record RegisterRequest(string UserName, string Email, string Password);

public record LoginRequest(string UserName, string Password);

public record AuthResponse(string Token, string UserId, string UserName);

// Результат Api.RegisterAsync/LoginAsync: либо Response, либо человекочитаемый Error из ValidationProblemDetails.
public record AuthResult(AuthResponse? Response, string? Error);

// Форма ответа Results.ValidationProblem(...) — { "errors": { "Code": ["message"] } }.
public record ValidationProblemResponse(Dictionary<string, string[]>? Errors);

public record CreateRoomRequest(string Name, bool IsPrivate);

public record JoinRoomRequest(Guid? RoomId, string? InviteCode);

public record RoomResponse(
    Guid Id,
    string Name,
    bool IsPrivate,
    string? InviteCode,
    DateTimeOffset CreatedAt,
    string OwnerId);

public record RoomMemberResponse(string UserId, string UserName, DateTimeOffset JoinedAt);

public record MessageResponse(
    long Id,
    Guid RoomId,
    string UserId,
    string UserName,
    string Text,
    DateTimeOffset CreatedAt);

// SignalR-уведомления от ChatHub.
public record UserTypingNotification(Guid RoomId, string UserId, string UserName);

public record PresenceNotification(string UserId, string UserName, bool IsOnline);
