namespace ChatRoomNET.Web.Domain;

public class Message
{
    public long Id { get; set; }

    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public string Text { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
