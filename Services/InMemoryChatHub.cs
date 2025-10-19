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

    // In-memory history of recent chat messages
    private const int MaxHistory = 200; // store more than 20 so we can serve flexible counts
    private readonly List<ChatMessage> _history = new();
    private readonly object _historyLock = new();
    
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
            // Use non-canceling token for server/system broadcasts
            await SendSystemEventAsync($"Client connected, id = {id}");
            
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
                    await BroadcastTextAsync(message, id, cancellationToken: CancellationToken.None);

                    // 2) Build and send a server response as a separate system message
                    var handler = _serviceProvider.GetRequiredService<IChatResponseHandler>();
                    var response = await handler.BuildResponseAsync(message, id, cancellationToken);
                    if (!string.IsNullOrEmpty(response))
                    {
                        await SendSystemAsync(response);
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
            // Broadcast disconnect with non-canceling token
            await SendSystemEventAsync($"Client disconnected, id = {id}");
        }
    }
    
    public async Task BroadcastTextAsync(string message, string? senderId, ChatMessage.MessageType type = ChatMessage.MessageType.Message, CancellationToken cancellationToken = default)
    {
        var sender = senderId ?? "System";
        var msgObj = new ChatMessage { Id = sender, Text = message, Type = type, TS = DateTimeOffset.UtcNow };

        // Store in history (keep only the most recent MaxHistory messages)
        lock (_historyLock)
        {
            _history.Add(msgObj);
            if (_history.Count > MaxHistory)
            {
                var removeCount = _history.Count - MaxHistory;
                _history.RemoveRange(0, removeCount);
            }
        }

        var payload = JsonSerializer.Serialize(msgObj);
        var bytes = Encoding.UTF8.GetBytes(payload);
        var segment = new ArraySegment<byte>(bytes);
        foreach (var kvp in _sockets)
        {
            var socket = kvp.Value;
            if (socket.State == WebSocketState.Open)
            {
                try
                {
                    // Use a non-canceling token for sends to decouple from per-request tokens
                    await socket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch (OperationCanceledException)
                {
                    // Treat cancellation as a skipped send, do not close the socket
                    continue;
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

    /*
    private Task SendSystemAsync(string text, ChatMessage.MessageType type, CancellationToken cancellationToken)
        => BroadcastTextAsync(text, senderId: null, type: type, CancellationToken.None);
    */
    
    // Overload without token for convenience and to avoid passing per-request tokens
    private Task SendSystemAsync(string text, ChatMessage.MessageType type = ChatMessage.MessageType.Message)
        => BroadcastTextAsync(text, senderId: null, type: type, CancellationToken.None);

    private Task SendSystemEventAsync(string text)
        => SendSystemAsync(text, ChatMessage.MessageType.Event);
    
    public ClientContext GetOrCreateContext(string clientId)
    {
        return _contexts.GetOrAdd(clientId, _ => new ClientContext());
    }

    public bool TryGetContext(string clientId, out ClientContext? context)
    {
        return _contexts.TryGetValue(clientId, out context);
    }

    public IReadOnlyList<ChatMessage> GetHistory(int count = 20)
    {
        lock (_historyLock)
        {
            var take = Math.Min(count, _history.Count);
            if (take <= 0) return Array.Empty<ChatMessage>();
            return _history.GetRange(_history.Count - take, take).ToArray();
        }
    }
}
