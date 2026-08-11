using ChatRoomNET.Web.Contracts;
using ChatRoomNET.Web.Data;
using ChatRoomNET.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace ChatRoomNET.Web.Services;

public interface IRoomService
{
    Task<RoomResponse> CreateAsync(string ownerId, CreateRoomRequest request);
    Task<IReadOnlyList<RoomResponse>> GetMyRoomsAsync(string userId);
    Task<RoomResponse?> JoinAsync(string userId, JoinRoomRequest request);
    Task<IReadOnlyList<RoomMemberResponse>?> GetMembersAsync(Guid roomId, string userId);
}

public class RoomService(ChatDbContext db) : IRoomService
{
    public async Task<RoomResponse> CreateAsync(string ownerId, CreateRoomRequest request)
    {
        var now = DateTimeOffset.UtcNow;
        var room = new Room
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            IsPrivate = request.IsPrivate,
            InviteCode = GenerateInviteCode(),
            CreatedAt = now,
            OwnerId = ownerId
        };
        room.Members.Add(new RoomMember { RoomId = room.Id, UserId = ownerId, JoinedAt = now });

        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        return ToResponse(room);
    }

    public async Task<IReadOnlyList<RoomResponse>> GetMyRoomsAsync(string userId)
    {
        return await db.RoomMembers
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.Room.CreatedAt)
            .Select(m => new RoomResponse(
                m.Room.Id, m.Room.Name, m.Room.IsPrivate, m.Room.InviteCode, m.Room.CreatedAt, m.Room.OwnerId))
            .ToListAsync();
    }

    public async Task<RoomResponse?> JoinAsync(string userId, JoinRoomRequest request)
    {
        Room? room = null;
        if (!string.IsNullOrWhiteSpace(request.InviteCode))
        {
            room = await db.Rooms.FirstOrDefaultAsync(r => r.InviteCode == request.InviteCode);
        }
        else if (request.RoomId is { } roomId)
        {
            // По id можно вступить только в публичную комнату; приватную не раскрываем.
            room = await db.Rooms.FirstOrDefaultAsync(r => r.Id == roomId && !r.IsPrivate);
        }

        if (room is null)
        {
            return null;
        }

        var alreadyMember = await db.RoomMembers.AnyAsync(m => m.RoomId == room.Id && m.UserId == userId);
        if (!alreadyMember)
        {
            db.RoomMembers.Add(new RoomMember { RoomId = room.Id, UserId = userId, JoinedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        return ToResponse(room);
    }

    public async Task<IReadOnlyList<RoomMemberResponse>?> GetMembersAsync(Guid roomId, string userId)
    {
        var isMember = await db.RoomMembers.AnyAsync(m => m.RoomId == roomId && m.UserId == userId);
        if (!isMember)
        {
            return null;
        }

        return await db.RoomMembers
            .Where(m => m.RoomId == roomId)
            .OrderBy(m => m.JoinedAt)
            .Select(m => new RoomMemberResponse(m.UserId, m.User.UserName!, m.JoinedAt))
            .ToListAsync();
    }

    private static string GenerateInviteCode() =>
        Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private static RoomResponse ToResponse(Room room) =>
        new(room.Id, room.Name, room.IsPrivate, room.InviteCode, room.CreatedAt, room.OwnerId);
}
