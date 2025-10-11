using System.Threading;
using System.Threading.Tasks;

namespace CoreServer.Logic;

public class GPTChatResponseHandler : IChatResponseHandler
{
    public Task<string> BuildResponseAsync(string message, string clientId, CancellationToken cancellationToken = default)
    {
        // Simple default implementation returns a static acknowledgement
        return Task.FromResult("received");
    }
}