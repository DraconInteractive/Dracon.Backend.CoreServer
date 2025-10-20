using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using CoreServer.Models;

namespace CoreServer.Services;

public interface IChatHub
{
    Task HandleConnectionAsync(WebSocket socket, CancellationToken cancellationToken = default);
    Task BroadcastTextAsync(string message, string? senderId, ChatMessage.MessageType type, CancellationToken cancellationToken = default);

    // History
    IReadOnlyList<CoreServer.Models.ChatMessage> GetHistory(int count = 20, bool includeEvents = false);

    // Client context accessors
    CoreServer.Models.ClientContext GetOrCreateContext(string clientId);
    bool TryGetContext(string clientId, out CoreServer.Models.ClientContext? context);
}
