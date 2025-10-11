using System.Threading;
using System.Threading.Tasks;

namespace CoreServer.Logic;

public interface IChatResponseHandler
{
    Task<string> BuildResponseAsync(string message, string clientId, CancellationToken cancellationToken = default);
}
