using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace CoreServer.Services;

public class InMemoryChatHub : IChatHub
{
    private readonly ConcurrentDictionary<string, WebSocket> _sockets = new();

    public async Task HandleConnectionAsync(WebSocket socket, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid().ToString("N");
        _sockets[id] = socket;
        try
        {
            await SendSystemAsync($"client:{id} connected", cancellationToken);

            var buffer = new byte[4 * 1024];
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await BroadcastTextAsync(message, id, cancellationToken);
                }
            }
        }
        finally
        {
            _sockets.TryRemove(id, out _);
            if (socket.State != WebSocketState.Closed && socket.State != WebSocketState.Aborted)
            {
                try { await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None); } catch { /* ignore */ }
            }
            await SendSystemAsync($"client:{id} disconnected", cancellationToken);
        }
    }

    public async Task BroadcastTextAsync(string message, string? senderId, CancellationToken cancellationToken = default)
    {
        var sender = senderId is null ? "system" : "client";
        var id = senderId ?? string.Empty;
        var payload = JsonSerializer.Serialize(new { sender, id, text = message, ts = DateTimeOffset.UtcNow });
        var bytes = Encoding.UTF8.GetBytes(payload);
        var segment = new ArraySegment<byte>(bytes);
        foreach (var kvp in _sockets)
        {
            var socket = kvp.Value;
            if (socket.State == WebSocketState.Open)
            {
                try
                {
                    await socket.SendAsync(segment, WebSocketMessageType.Text, true, cancellationToken);
                }
                catch
                {
                    // best effort - drop broken socket
                    _ = socket.CloseAsync(WebSocketCloseStatus.InternalServerError, "Send failed", CancellationToken.None);
                    _sockets.TryRemove(kvp.Key, out _);
                }
            }
        }
    }

    private Task SendSystemAsync(string text, CancellationToken cancellationToken)
        => BroadcastTextAsync(text, senderId: null, cancellationToken);
}
