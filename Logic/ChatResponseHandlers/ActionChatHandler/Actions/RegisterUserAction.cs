using System.Text.RegularExpressions;
using CoreServer.Models;
using CoreServer.Services;

namespace CoreServer.Logic;

public class RegisterUserAction : IChatAction
{
    private readonly IAuthService _auth;
    public RegisterUserAction(IAuthService auth)
    {
        _auth = auth;
    }

    public string Name => "Register";
    
    public Regex? Pattern { get; } = null;

    public string StrictPattern { get; } = "register";

    public Task<string?> ExecuteAsync(Match match, string originalMessage, string clientId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }

    public async Task<string?> ExecuteAsync(string originalMessage, StrictCommand command, string clientId, CancellationToken cancellationToken = default)
    {
        var response = $"Success";

        if (command.Args.Length == 0)
        {
            response = "Error: Register requires arguments for username and password (-u | -p)";
            return response;
        }

        bool useJson = command.Args.Contains("-j");
        
        // get user name
        var uIndex = Array.IndexOf(command.Args, "-u");
        if (uIndex == -1 || command.Args.Length == uIndex - 1) // Either -u doesnt exist, or its the last element
        {
            response = useJson ? new JsonReturnPacket(clientId, "Error: Missing username (-u)", true).GetJson() : "Error: Missing username (-u)";
            return response;
        }

        if (command.Args[uIndex + 1][0] == '-') // Different parameter start instead of actual value
        {
            response = useJson ? new JsonReturnPacket(clientId, "Error: Username has no parameter, or invalid parameter", true).GetJson() : "Error: Username has no parameter, or invalid parameter";
            return response;
        }

        var username = command.Args[uIndex + 1];

        var pIndex = Array.IndexOf(command.Args, "-p");
        if (pIndex == -1 || command.Args.Length == pIndex - 1)
        {
            response = useJson ? new JsonReturnPacket(clientId, "Error: Missing password (-p)", true).GetJson() : "Error: Missing password (-p)";
            return response;
        }
        
        if (command.Args[pIndex + 1][0] == '-')
        {
            response = useJson ? new JsonReturnPacket(clientId, "Error: Password has no parameter, or invalid parameter", true).GetJson() : "Error: Password has no parameter, or invalid parameter";
            return response;
        }

        var password = command.Args[pIndex + 1];

        // Call auth service to register (Email optional; not provided via args here)
        var (ok, error) = await _auth.RegisterAsync(username, password, null, cancellationToken);
        if (!ok)
        {
            var errMsg = error ?? "Registration failed";
            response = useJson ? new JsonReturnPacket(clientId, errMsg, true).GetJson() : errMsg;
            return response;
        }

        var rMessage = $"User registration successful";
        response = useJson ? new JsonReturnPacket(clientId, rMessage, false).GetJson() : rMessage;

        return response;
    }
}