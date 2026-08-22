namespace ChatRoomNET.Web.Contracts;

public record MessageResponse(
    long Id,
    Guid RoomId,
    string UserId,
    string UserName,
    string Text,
    DateTimeOffset CreatedAt);
