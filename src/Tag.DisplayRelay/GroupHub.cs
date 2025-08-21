using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;

namespace Tag.DisplayRelay;

// Group - List of conns by - {siteId}:{roomId}
public class GroupHub
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<WebSocket, byte>> _groups = new();

    public void Add(string group, WebSocket socket) =>
        _groups.GetOrAdd(group, _ => new()).TryAdd(socket, 0);

    public void Remove(string group, WebSocket socket)
    {
        if (_groups.TryGetValue(group, out var set)) set.TryRemove(socket, out _);
    }

    public async Task BroadcastAsync(string group, object message)
    {
        if (!_groups.TryGetValue(group, out var set)) return;

        var bytes = JsonSerializer.SerializeToUtf8Bytes(message);
        var seg = new ArraySegment<byte>(bytes);

        foreach (var s in set.Keys)
        {
            if (s.State == WebSocketState.Open)
            {
                try { await s.SendAsync(seg, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None); }
                catch { /* ignore; client will reconnect */ }
            }
        }
    }
}
