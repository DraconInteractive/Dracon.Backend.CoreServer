using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace CoreServer.Services;

public interface IChatHub
{
    Task HandleConnectionAsync(WebSocket socket, CancellationToken cancellationToken = default);
    Task BroadcastTextAsync(string message, CancellationToken cancellationToken = default);
}
