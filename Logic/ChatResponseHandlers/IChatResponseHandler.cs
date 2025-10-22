using System.Threading;
using System.Threading.Tasks;
using CoreServer.Models;

namespace CoreServer.Logic;

public interface IChatResponseHandler
{
    Task<ChatResponse> BuildResponseAsync(string message, string clientId, CancellationToken cancellationToken = default);
}
