using System.Threading;
using System.Threading.Tasks;
using CoreServer.Models;

namespace CoreServer.Logic;

public class DefaultChatResponseHandler : IChatResponseHandler
{
    public Task<ChatResponse> BuildResponseAsync(string message, string clientId, CancellationToken cancellationToken = default)
    {
        // Simple default implementation returns a static acknowledgement
        return Task.FromResult(new ChatResponse("<<>>"));
    }
}
