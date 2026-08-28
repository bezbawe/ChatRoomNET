using ChatRoomNET.Web.Contracts;
using ChatRoomNET.Web.Data;
using ChatRoomNET.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace ChatRoomNET.Web.Services;

public interface IMessageService
{
    Task<IReadOnlyList<MessageResponse>?> GetHistoryAsync(Guid roomId, string userId, long? before, int take);
    Task<MessageResponse?> SendAsync(Guid roomId, string userId, string text);
}

public class MessageService(ChatDbContext db) : IMessageService
{
    public const int DefaultTake = 30;
    public const int MaxTake = 100;
    public const int MaxTextLength = 2000; // совпадает с Message.Text HasMaxLength в ChatDbContext

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

    // Сохраняет сообщение автора-участника и возвращает его для broadcast'а. null — если
    // пользователь не в комнате или текст пустой/слишком длинный.
    public async Task<MessageResponse?> SendAsync(Guid roomId, string userId, string text)
    {
        text = text.Trim();
        if (text.Length == 0 || text.Length > MaxTextLength)
        {
            return null;
        }

        var isMember = await db.RoomMembers.AnyAsync(m => m.RoomId == roomId && m.UserId == userId);
        if (!isMember)
        {
            return null;
        }

        var message = new Message
        {
            RoomId = roomId,
            UserId = userId,
            Text = text,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Messages.Add(message);
        await db.SaveChangesAsync();

        // Достаём имя автора из БД — так же, как в GetHistoryAsync (не доверяем внешним данным).
        return await db.Messages
            .Where(m => m.Id == message.Id)
            .Select(m => new MessageResponse(m.Id, m.RoomId, m.UserId, m.User.UserName!, m.Text, m.CreatedAt))
            .FirstAsync();
    }
}
