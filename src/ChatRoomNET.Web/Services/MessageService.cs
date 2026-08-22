using ChatRoomNET.Web.Contracts;
using ChatRoomNET.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace ChatRoomNET.Web.Services;

public interface IMessageService
{
    Task<IReadOnlyList<MessageResponse>?> GetHistoryAsync(Guid roomId, string userId, long? before, int take);
}

public class MessageService(ChatDbContext db) : IMessageService
{
    public const int DefaultTake = 30;
    public const int MaxTake = 100;

    // Keyset-пагинация по монотонному Id (эквивалентно CreatedAt, но без коллизий по времени).
    // Курсор `before` — Id самого старого уже загруженного сообщения; возвращаем страницу более старых.
    public async Task<IReadOnlyList<MessageResponse>?> GetHistoryAsync(Guid roomId, string userId, long? before, int take)
    {
        var isMember = await db.RoomMembers.AnyAsync(m => m.RoomId == roomId && m.UserId == userId);
        if (!isMember)
        {
            return null;
        }

        take = Math.Clamp(take, 1, MaxTake);

        var query = db.Messages.Where(m => m.RoomId == roomId);
        if (before is { } cursor)
        {
            query = query.Where(m => m.Id < cursor);
        }

        var page = await query
            .OrderByDescending(m => m.Id)
            .Take(take)
            .Select(m => new MessageResponse(m.Id, m.RoomId, m.UserId, m.User.UserName!, m.Text, m.CreatedAt))
            .ToListAsync();

        // Отдаём по возрастанию (старые → новые), удобно для дозагрузки вверх в infinite scroll.
        page.Reverse();
        return page;
    }
}
