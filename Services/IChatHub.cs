using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace CoreServer.Services;

public interface IChatHub
{
    Task HandleConnectionAsync(WebSocket socket, CancellationToken cancellationToken = default);
    Task BroadcastTextAsync(string message, string? senderId, CancellationToken cancellationToken = default);

    // Client context accessors
    CoreServer.Models.ClientContext GetOrCreateContext(string clientId);
    bool TryGetContext(string clientId, out CoreServer.Models.ClientContext? context);
}
