using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace CoreServer.Services;

public interface IChatHub
{
    Task HandleConnectionAsync(WebSocket socket, CancellationToken cancellationToken = default);
    Task BroadcastTextAsync(string message, string? senderId, CancellationToken cancellationToken = default);

    // History
    IReadOnlyList<CoreServer.Models.ChatMessage> GetHistory(int count = 20);

    // Client context accessors
    CoreServer.Models.ClientContext GetOrCreateContext(string clientId);
    bool TryGetContext(string clientId, out CoreServer.Models.ClientContext? context);
}
