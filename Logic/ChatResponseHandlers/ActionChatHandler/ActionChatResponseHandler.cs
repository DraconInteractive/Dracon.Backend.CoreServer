using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CoreServer.Logic;

public class ActionChatResponseHandler : IChatResponseHandler
{
    private readonly IChatAction[] _actions;

    public ActionChatResponseHandler(IEnumerable<IChatAction> actions)
    {
        _actions = actions.ToArray();
    }

    public async Task<string> BuildResponseAsync(string message, string clientId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;

        var text = message.Trim();

        bool strict = text[0] == '/';
        string strictTrigger = strict ? text.TrimStart('/').Split(' ')[0] : "";
        foreach (var action in _actions)
        {
            if (strict && !string.IsNullOrEmpty(strictTrigger))
            {
                var strictMatch = action.StrictPattern == strictTrigger;
                if (strictMatch)
                {
                    var result = await action.ExecuteAsync(text.TrimStart('/'), clientId, cancellationToken).ConfigureAwait(false);
                    return string.IsNullOrWhiteSpace(result) ? string.Empty : result!;
                }

                continue;
            }
            
            var match = action.Pattern.Match(text);
            if (match.Success)
            {
                var result = await action.ExecuteAsync(match, text, clientId, cancellationToken).ConfigureAwait(false);
                return string.IsNullOrWhiteSpace(result) ? string.Empty : result!;
            }
        }

        // Fallback when no action matches
        return strict ? "No matching action found." : "Message received.";
    }
}