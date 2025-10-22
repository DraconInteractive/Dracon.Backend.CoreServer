using System.Text.RegularExpressions;
using CoreServer.Models;

namespace CoreServer.Logic;

public class LoginUserAction : IChatAction
{
    public string Name => "Login";

    public Regex? Pattern { get; } = null;

    public string StrictPattern { get; } = "login";

    public Task<string?> ExecuteAsync(Match match, string originalMessage, string clientId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }

    public Task<string?> ExecuteAsync(string originalMessage, StrictCommand command, string clientId, CancellationToken cancellationToken = default)
    {
        var response = $"Success";

        if (command.Args.Length == 0)
        {
            response = "Error: Register requires arguments for username and password (-u | -p)";
            return Task.FromResult<string?>(response);
        }

        bool useJson = command.Args.Contains("-j");
        
        // get user name
        var uIndex = Array.IndexOf(command.Args, "-u");
        if (uIndex == -1 || command.Args.Length == uIndex - 1) // Either -u doesnt exist, or its the last element
        {
            response = useJson ? new JsonReturnPacket(clientId, "Error: Missing username (-u)", true).GetJson() : "Error: Missing username (-u)";
            return Task.FromResult<string?>(response);
        }

        if (command.Args[uIndex + 1][0] == '-') // Different parameter start instead of actual value
        {
            response = useJson ? new JsonReturnPacket(clientId, "Error: Username has no parameter, or invalid parameter", true).GetJson() : "Error: Username has no parameter, or invalid parameter";
            return Task.FromResult<string?>(response);
        }

        var username = command.Args[uIndex + 1];

        var pIndex = Array.IndexOf(command.Args, "-p");
        if (pIndex == -1 || command.Args.Length == pIndex - 1)
        {
            response = useJson ? new JsonReturnPacket(clientId, "Error: Missing password (-p)", true).GetJson() : "Error: Missing password (-p)";
            return Task.FromResult<string?>(response);
        }
        
        if (command.Args[pIndex + 1][0] == '-')
        {
            response = useJson ? new JsonReturnPacket(clientId, "Error: Password has no parameter, or invalid parameter", true).GetJson() : "Error: Password has no parameter, or invalid parameter";
            return Task.FromResult<string?>(response);
        }

        var password = command.Args[pIndex + 1];

        // Temp return until login implemented
        var rMessage = $"User login successful";
        response = useJson ? new JsonReturnPacket(clientId, rMessage, false).GetJson() : rMessage;

        return Task.FromResult<string?>(response);
    }
}