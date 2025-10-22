using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using CoreServer.Logic;
using CoreServer.Models;
using Microsoft.Extensions.DependencyInjection;
using CoreServer.Services;

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
                    var incoming = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var outgoingText = incoming;
                    var senderLabel = id;
                    // Try parse JSON payload with optional token: { "text": "...", "token": "..." }
                    try
                    {
                        using var doc = JsonDocument.Parse(incoming);
                        if (doc.RootElement.ValueKind == JsonValueKind.Object)
                        {
                            if (doc.RootElement.TryGetProperty("text", out var textEl) &&
                                textEl.ValueKind == JsonValueKind.String)
                            {
                                outgoingText = textEl.GetString() ?? string.Empty;
                            }

                            if (doc.RootElement.TryGetProperty("token", out var tokenEl) &&
                                tokenEl.ValueKind == JsonValueKind.String)
                            {
                                var token = tokenEl.GetString();
                                if (!string.IsNullOrWhiteSpace(token))
                                {
                                    var tokenService = _serviceProvider.GetService<ITokenService>();
                                    var principal = tokenService?.ValidateToken(token!);
                                    if (principal != null)
                                    {
                                        var ctx = GetOrCreateContext(id);
                                        ctx.UserId = principal.FindFirst("sub")?.Value ?? principal
                                            .FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
                                            ?.Value;
                                        ctx.UserName = principal.FindFirst("name")?.Value ?? principal.Identity?.Name;
                                    }
                                }
                            }
                        }
                    }
                    catch (JsonException jEx)
                    {
                        // ignore; will be treated as plain-text
                    }
                    catch (OperationCanceledException opex)
                    {
                        // ignore; loop will check cancellation and exit if needed
                    }
                    catch (Exception ex)
                    {
                        // Log and keep the socket alive
                        Console.WriteLine($"[WS] Error processing message: {ex}");
                        await SendSystemAsync($"An error occurred while processing your message: {ex}", ChatMessage.MessageType.Message, id);
                    }

                    if (TryGetContext(id, out var context))
                    {
                        if (!string.IsNullOrWhiteSpace(context?.UserName)) senderLabel = context!.UserName!;
                        else if (!string.IsNullOrWhiteSpace(context?.UserId)) senderLabel = context!.UserId!;
                    }

                    try
                    {
                        // Build response from chat handler
                        var chatHandler = _serviceProvider.GetRequiredService<IChatResponseHandler>();
                        var systemResponse = await chatHandler.BuildResponseAsync(outgoingText, id, cancellationToken);
                        
                        // Echo/broadcast the text to all clients (including sender)
                        // If override exists, replace echo with this. Used for masking client side actions
                        if (systemResponse?.EchoOverride != null)
                        {
                            if (!string.IsNullOrEmpty(systemResponse.EchoOverride))
                            {
                                await BroadcastTextAsync(systemResponse.EchoOverride, senderLabel, cancellationToken: CancellationToken.None);
                            }
                        }
                        else
                        {
                            await BroadcastTextAsync(outgoingText, senderLabel, cancellationToken: CancellationToken.None);
                        }
                    
                        // Send built response
                        if (!string.IsNullOrEmpty(systemResponse?.ResponseText))
                        {
                            await SendSystemAsync(systemResponse.ResponseText, ChatMessage.MessageType.Message, id);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log and keep the socket alive
                        Console.WriteLine($"[WS] Error building response message: {ex}");
                        await SendSystemAsync($"An error occurred while building your response: {ex}", ChatMessage.MessageType.Message, id);
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
        var msgObj = new ChatMessage { ClientId = sender, Text = message, Type = type, TS = DateTimeOffset.UtcNow };

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
    
    // Targeted send to a single client (does not add to public history)
    private async Task SendToClientAsync(string clientId, string text, ChatMessage.MessageType type = ChatMessage.MessageType.Message)
    {
        if (_sockets.TryGetValue(clientId, out var socket) && socket.State == WebSocketState.Open)
        {
            var msgObj = new ChatMessage { ClientId = "System", Text = text, Type = type, TS = DateTimeOffset.UtcNow };
            var payload = JsonSerializer.Serialize(msgObj);
            var bytes = Encoding.UTF8.GetBytes(payload);
            try
            {
                await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            catch
            {
                try { await socket.CloseAsync(WebSocketCloseStatus.InternalServerError, "Send failed", CancellationToken.None); } catch { }
                // Remove broken socket
                foreach (var kvp in _sockets)
                {
                    if (ReferenceEquals(kvp.Value, socket)) { _sockets.TryRemove(kvp.Key, out _); break; }
                }
            }
        }
    }

    // System message helper: if targetClientId provided, send privately; otherwise broadcast to all
    private Task SendSystemAsync(string text, ChatMessage.MessageType type = ChatMessage.MessageType.Message, string? targetClientId = null)
    {
        if (!string.IsNullOrEmpty(targetClientId))
        {
            return SendToClientAsync(targetClientId, text, type);
        }
        return BroadcastTextAsync(text, senderId: null, type: type, CancellationToken.None);
    }

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

    public IReadOnlyList<ChatMessage> GetHistory(int count = 20, bool includeEvents = false)
    {
        lock (_historyLock)
        {
            if (_history.Count == 0 || count <= 0) return Array.Empty<ChatMessage>();

            if (includeEvents)
            {
                var take = Math.Min(count, _history.Count);
                return _history.GetRange(_history.Count - take, take).ToArray();
            }

            // When onlyMessages is true, collect the last `count` entries that are Message type
            var result = new List<ChatMessage>(Math.Min(count, _history.Count));
            for (int i = _history.Count - 1; i >= 0 && result.Count < count; i--)
            {
                var m = _history[i];
                if (m.Type != ChatMessage.MessageType.Event)
                {
                    result.Add(m);
                }
            }
            result.Reverse(); // chronological order
            return result.ToArray();
        }
    }
}
