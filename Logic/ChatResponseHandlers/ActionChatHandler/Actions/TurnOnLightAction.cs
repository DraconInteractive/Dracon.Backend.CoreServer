using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CoreServer.Logic;

public class TurnOnLightAction : IChatAction
{
    public string Name => "TurnOnLight";

    // Matches variants like:
    //  - "Turn on the bedroom light"
    //  - "switch on the kitchen lights"
    //  - allows optional trailing period
    public Regex? Pattern { get; } = new(
        pattern: "^\\s*(turn\\s+on|switch\\s+on)\\s+the\\s+(?<room>[a-zA-Z][a-zA-Z0-9_-]*)\\s+(?<device>light|lights)\\s*\\.?\\s*$",
        options: RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public string StrictPattern { get; } = "togdev";

    public Task<string?> ExecuteAsync(Match match, string originalMessage, string clientId, CancellationToken cancellationToken = default)
    {
        var device = match.Groups["device"].Value;

        // Here we would perform the real action; for now we simulate success.
        var singularDevice = device.EndsWith("s", System.StringComparison.OrdinalIgnoreCase)
            ? device.Substring(0, device.Length - 1)
            : device;

        var response = $"{singularDevice} turned on";
        return Task.FromResult<string?>(response);
    }

    public Task<string?> ExecuteAsync(string originalMessage, string clientId, CancellationToken cancellationToken = default)
    {
        // strict pattern example:
        // /togdev "bedroom 1" light on
        // => trigger = togdev, args = [location, device, state]
        if (!StrictCommand.TryParse(originalMessage, out var cmd) || cmd == null)
        {
            return Task.FromResult<string?>("Invalid command structure. Command structure: /togdev location device state.");
        }

        var args = cmd.Value.Args;
        // After ActionChatResponseHandler strips '/', TryParse receives only text.
        // Args are expected to be exactly: [location, device, state]
        if (args.Length != 3)
        {
            return Task.FromResult<string?>("Invalid command structure. Command structure: /togdev location device state.");
        }

        string location = args[0];
        string device = args[1];
        string state = args[2];

        var response = $"Setting the {device} in the {location} to {state}";
        return Task.FromResult<string?>(response);
    }
}
