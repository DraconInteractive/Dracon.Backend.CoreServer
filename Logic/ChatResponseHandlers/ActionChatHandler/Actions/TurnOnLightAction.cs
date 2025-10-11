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
    public Regex Pattern { get; } = new(
        pattern: "^\\s*(turn\\s+on|switch\\s+on)\\s+the\\s+(?<room>[a-zA-Z][a-zA-Z0-9_-]*)\\s+(?<device>light|lights)\\s*\\.?\\s*$",
        options: RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
}
