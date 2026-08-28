namespace ChatRoomNET.Web.Contracts;

public record UserTypingNotification(Guid RoomId, string UserId, string UserName);

public record PresenceNotification(string UserId, string UserName, bool IsOnline);
