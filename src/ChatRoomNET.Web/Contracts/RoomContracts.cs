namespace ChatRoomNET.Web.Contracts;

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
