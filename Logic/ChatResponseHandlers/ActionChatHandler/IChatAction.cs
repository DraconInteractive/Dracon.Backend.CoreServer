using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CoreServer.Logic;

public interface IChatAction
{
    string Name { get; }
    Regex Pattern { get; }
    Task<string?> ExecuteAsync(
        Match match,
        string originalMessage,
        string clientId,
        CancellationToken cancellationToken = default);
}
