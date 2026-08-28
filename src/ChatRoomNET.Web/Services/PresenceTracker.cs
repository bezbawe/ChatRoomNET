using System.Collections.Concurrent;

namespace ChatRoomNET.Web.Services;

public interface IPresenceTracker
{
    // true — если это первое подключение пользователя (перешёл в онлайн).
    bool Connect(string userId, string connectionId);

    // true — если закрылось последнее подключение пользователя (перешёл в оффлайн).
    bool Disconnect(string userId, string connectionId);
}

// В памяти: userId -> набор его подключений. Несколько вкладок/реконнекты не «моргают»
// статусом: онлайн-переход считаем только по первому коннекту, оффлайн — по последнему.
// Singleton, поэтому доступ к наборам синхронизируем.
public class PresenceTracker : IPresenceTracker
{
    private readonly ConcurrentDictionary<string, HashSet<string>> _connections = new();

    public bool Connect(string userId, string connectionId)
    {
        var connections = _connections.GetOrAdd(userId, _ => new HashSet<string>());
        lock (connections)
        {
            var wasOffline = connections.Count == 0;
            connections.Add(connectionId);
            return wasOffline;
        }
    }

    public bool Disconnect(string userId, string connectionId)
    {
        if (!_connections.TryGetValue(userId, out var connections))
        {
            return false;
        }

        lock (connections)
        {
            connections.Remove(connectionId);
            return connections.Count == 0;
        }
    }
}
