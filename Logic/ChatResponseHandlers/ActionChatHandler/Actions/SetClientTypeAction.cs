using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CoreServer.Logic;

public class SetClientTypeAction : IChatAction
{
    public string Name => "SetClientType";

    // Null pattern means action should be ignored in informal text
    public Regex? Pattern { get; } = null;

    public string StrictPattern { get; } = "client";

    public Task<string?> ExecuteAsync(Match match, string originalMessage, string clientId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }

    public Task<string?> ExecuteAsync(string originalMessage, string clientId, CancellationToken cancellationToken = default)
    {
        var parts = originalMessage.Split(' ');
        if (parts[1] == "set")
        {
            if (parts[2] == "type")
            {
                // set client type to parts[3]
            }
        }
        var response = $"Hello!";
        return Task.FromResult<string?>(response);
    }
}