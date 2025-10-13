using System.Text.Json;
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

        var useJson = tokens.Contains("-j");
        var invalidUsageJsonError = new ReturnPacket(clientId,
            $"Invalid argument(s). Usage: \n/client set type <type>\n/client get type", true);

        var invalidUsageError = useJson ? JsonSerializer.Serialize(invalidUsageJsonError) : invalidUsageJsonError.Message;
        
        if (!string.Equals(tokens[0], "set", StringComparison.OrdinalIgnoreCase) && !string.Equals(tokens[0], "get", StringComparison.OrdinalIgnoreCase))
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
                var setReturn = JsonSerializer.Serialize(new SetReturnPacket(clientId, "Success", false, tokens[2]));
                
                return Task.FromResult<string?>(useJson ? setReturn : "Success");
            case "get":
                if (tokens.Length < 2 || !string.Equals(tokens[1], "type", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(invalidUsageError);
                }

                return Task.FromResult<string?>(useJson ? JsonSerializer.Serialize(new GetReturnPacket(clientId, "Success", false, tokens[2])) : "Success");
            default:
                return Task.FromResult(invalidUsageError);
        }
    }

    [Serializable]
    public class ReturnPacket
    {
        public string? ClientId { get; set; }
        public string? Message { get; set; }
        public bool Error { get; set; }

        public ReturnPacket(string clientId, string message, bool error)
        {
            ClientId = clientId;
            Message = message;
            Error = error;
        }
    }

    [Serializable]
    public class SetReturnPacket : ReturnPacket
    {
        public string? Type { get; set; }

        public SetReturnPacket(string clientId, string message, bool error, string? type)
            : base(clientId, message, error)
        {
            Type = type;
        }
    }

    [Serializable]
    public class GetReturnPacket : ReturnPacket
    {
        public string? Type { get; set; }

        public GetReturnPacket(string clientId, string message, bool error, string? type)
            : base(clientId, message, error)
        {
            Type = type;
        }
    }
}