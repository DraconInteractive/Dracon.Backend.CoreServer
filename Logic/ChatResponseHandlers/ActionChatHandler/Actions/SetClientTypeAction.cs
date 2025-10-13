using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CoreServer.Services;

namespace CoreServer.Logic;

public class SetClientTypeAction : IChatAction
{
    private readonly IChatHub _hub;
    public SetClientTypeAction(IChatHub hub)
    {
        _hub = hub;
    }

    public string Name => "SetClientType";

    // Null pattern means action should be ignored in informal text
    public Regex? Pattern { get; } = null;

    public string StrictPattern { get; } = "client";

    public Task<string?> ExecuteAsync(Match match, string originalMessage, string clientId, CancellationToken cancellationToken = default)
    {
        // Not supported in informal mode
        return Task.FromResult<string?>(null);
    }

    public Task<string?> ExecuteAsync(string originalMessage, string clientId, CancellationToken cancellationToken = default)
    {
        // Strict syntax:
        // /client set type <type>
        // /client get type
        if (!StrictCommand.TryParse(originalMessage, out var cmd) || cmd == null)
        {
            return Task.FromResult<string?>("Invalid command. Usage: /client set type <type>");
        }

        var tokens = cmd.Value.Args;
        if (tokens.Length < 1)
        {
            return Task.FromResult<string?>("Invalid command. Usage: /client set|get type <type>");
        }

        if (!string.Equals(tokens[0], "set", StringComparison.OrdinalIgnoreCase) && !string.Equals(tokens[0], "get", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<string?>($"Unexpected argument {tokens[0]}. Usage: \n/client set type <type>\n/client get type");
        }
        
        var ctx = _hub.GetOrCreateContext(clientId);
        
        switch (tokens[0])
        {
            case "set":
                if (tokens.Length < 3)
                {
                    return Task.FromResult<string?>("Invalid command. Usage: /client set|get type <type>");
                }
                if (!string.Equals(tokens[1], "type", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult<string?>($"Unexpected argument {tokens[1]}. Usage: \n/client set type <type>\n/client get type");
                }
                ctx.ClientType = tokens[2];
                return Task.FromResult<string?>("Client type updated");
            case "get":
                if (tokens.Length < 2)
                {
                    return Task.FromResult<string?>("Invalid command. Usage: /client set|get type <type>");
                }
                if (!string.Equals(tokens[1], "type", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult<string?>($"Unexpected argument {tokens[1]}. Usage: \n/client set type <type>\n/client get type");
                }
                return Task.FromResult<string?>("Client type: " + ctx.ClientType);
            default:
                return Task.FromResult<string?>("Invalid command. Usage: \n/client set type <type>\n/client get type");
                break;
        }
    }
}