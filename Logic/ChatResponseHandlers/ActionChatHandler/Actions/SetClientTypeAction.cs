using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CoreServer.Models;
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

    // Strict syntax:
    // /client set type <type>
    // /client get type
    public Task<string?> ExecuteAsync(string originalMessage, StrictCommand command, string clientId, CancellationToken cancellationToken = default)
    {
        var tokens = command.Args;
        if (tokens.Length < 1)
        {
            return Task.FromResult<string?>("Invalid command. Usage: /client set|get type <type>");
        }

        var useJson = tokens.Contains("-j");
        var invalidUsageJsonError = new JsonReturnPacket(clientId,
            $"Invalid argument(s). Usage: \n/client set type <type>\n/client get type", true);

        var invalidUsageError = useJson ? invalidUsageJsonError.GetJson() : invalidUsageJsonError.Message;

        string[] validEntries = ["set", "get"];

        if (!validEntries.Contains(tokens[0]))
        {
            return Task.FromResult(invalidUsageError);
        }
        
        var ctx = _hub.GetOrCreateContext(clientId);
        
        switch (tokens[0])
        {
            case "set":
                if (tokens.Length < 3 || !string.Equals(tokens[1], "type", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(invalidUsageError);
                }
                
                ctx.ClientType = tokens[2];
                return Task.FromResult<string?>(useJson ? new SetReturnPacket(clientId, "Success", false, tokens[2]).GetJson() : "Success");
            case "get":
                if (tokens.Length < 2 || !string.Equals(tokens[1], "type", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(invalidUsageError);
                }

                var type = ctx.ClientType;
                return Task.FromResult<string?>(useJson ? new GetReturnPacket(clientId, "Success", false, type).GetJson() : "Success");
            default:
                return Task.FromResult(invalidUsageError);
        }
    }

    [Serializable]
    public class SetReturnPacket : JsonReturnPacket
    {
        public string? Type { get; set; }

        public SetReturnPacket(string clientId, string message, bool error, string? type)
            : base(clientId, message, error)
        {
            Type = type;
        }
    }

    [Serializable]
    public class GetReturnPacket : JsonReturnPacket
    {
        public string? Type { get; set; }

        public GetReturnPacket(string clientId, string message, bool error, string? type)
            : base(clientId, message, error)
        {
            Type = type;
        }
    }
}