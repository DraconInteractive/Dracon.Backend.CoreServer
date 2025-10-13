using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CoreServer.Models;

namespace CoreServer.Logic;

public class GreetingAction : IChatAction
{
    public string Name => "Greeting";

    // Matches common greetings like:
    //  - "hi", "hello", "hey", "howdy", "greetings", "yo", "sup"
    //  - "what's up", "what is up"
    //  - "hi there", "hello there"
    //  - "good morning/afternoon/evening/day"
    // Allows optional leading/trailing whitespace and optional trailing punctuation.
    public Regex? Pattern { get; } = new(
        pattern: @"^\s*(?:hi|hello|hey|howdy|greetings|yo|sup|what(?:'s| is)\s+up|hi\s+there|hello\s+there|good\s+(?:morning|afternoon|evening|day))\b[!.]?\s*$",
        options: RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public string StrictPattern { get; } = "greet";

    public Task<string?> ExecuteAsync(Match match, string originalMessage, string clientId, CancellationToken cancellationToken = default)
    {
        var response = $"Hello!";
        return Task.FromResult<string?>(response);
    }

    public Task<string?> ExecuteAsync(string originalMessage, StrictCommand command, string clientId, CancellationToken cancellationToken = default)
    {
        var response = $"Hello!";

        if (command.Args.Length > 0 && command.Args.Contains("-j"))
        {
            response = new JsonReturnPacket(clientId, "Hello!", false).GetJson();
        }
        return Task.FromResult<string?>(response);
    }
}