using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using CoreServer.Logic;
using CoreServer.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CoreServer.Services;

public class InMemoryChatHub : IChatHub
{
    private readonly ConcurrentDictionary<string, WebSocket> _sockets = new();
    private readonly ConcurrentDictionary<string, ClientContext> _contexts = new();
    
    private readonly IServiceProvider _serviceProvider;

    public InMemoryChatHub(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task HandleConnectionAsync(WebSocket socket, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid().ToString("N");
        _sockets[id] = socket;
        _contexts[id] = new ClientContext();
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
                    // 1) Echo/broadcast the original message to all clients (including sender)
                    await BroadcastTextAsync(message, id, cancellationToken);

                    // 2) Build and send a server response as a separate system message
                    var handler = _serviceProvider.GetRequiredService<IChatResponseHandler>();
                    var response = await handler.BuildResponseAsync(message, id, cancellationToken);
                    if (!string.IsNullOrEmpty(response))
                    {
                        await SendSystemAsync(response, cancellationToken);
                    }
                }
            }
        }
        finally
        {
            _sockets.TryRemove(id, out _);
            _contexts.TryRemove(id, out _);
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

    public ClientContext GetOrCreateContext(string clientId)
    {
        return _contexts.GetOrAdd(clientId, _ => new ClientContext());
    }

    public bool TryGetContext(string clientId, out ClientContext? context)
    {
        return _contexts.TryGetValue(clientId, out context);
    }
}
