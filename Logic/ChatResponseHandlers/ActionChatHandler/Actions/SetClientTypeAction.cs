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
        // Optional "to": /client set type to <type>
        if (!StrictCommand.TryParse(originalMessage, out var cmd) || cmd == null)
        {
            return Task.FromResult<string?>("Invalid command. Usage: /client set type <type>");
        }

        var tokens = cmd.Value.Args;
        // Expected tokens (Args) after trigger: set type [to] <type>
        if (tokens.Length < 3)
        {
            return Task.FromResult<string?>("Invalid command. Usage: /client set type <type>");
        }
        if (!string.Equals(tokens[0], "set", System.StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<string?>($"Unexpected argument {tokens[0]}. Usage: /client set type <type>");
        }
        if (!string.Equals(tokens[1], "type", System.StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<string?>($"Unexpected argument {tokens[1]}. Usage: /client set type <type>");
        }

        int typeIdx = 2;
        if (tokens.Length - typeIdx >= 2 && string.Equals(tokens[typeIdx], "to", System.StringComparison.OrdinalIgnoreCase))
        {
            typeIdx++;
        }
        if (typeIdx >= tokens.Length)
        {
            return Task.FromResult<string?>("Invalid command. Usage: /client set type <type>");
        }
        var typeValue = tokens[typeIdx];

        var ctx = _hub.GetOrCreateContext(clientId);
        ctx.ClientType = typeValue;

        return Task.FromResult<string?>("Client type updated");
    }
}