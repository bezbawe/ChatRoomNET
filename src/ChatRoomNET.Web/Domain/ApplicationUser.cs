using Microsoft.AspNetCore.Identity;

namespace ChatRoomNET.Web.Domain;

public class ApplicationUser : IdentityUser
{
    public ICollection<RoomMember> Memberships { get; set; } = new List<RoomMember>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
