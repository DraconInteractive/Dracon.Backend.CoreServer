using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CoreServer.Models;

namespace CoreServer.Logic;

public class ActionChatResponseHandler : IChatResponseHandler
{
    private readonly IChatAction[] _actions;

    public ActionChatResponseHandler(IEnumerable<IChatAction> actions)
    {
        _actions = actions.ToArray();
    }

    public async Task<ChatResponse> BuildResponseAsync(string message, string clientId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            return new ChatResponse(string.Empty);

        var text = message.Trim();

        bool strict = text.Length > 0 && text[0] == '/';
        
        if (strict)
        {
            text = text.TrimStart('/');
            if (!StrictCommand.TryParse(text, out var parsed) || parsed == null)
            {
                return new ChatResponse("Invalid strict command format.");
            }
            var strictTrigger = parsed.Value.Trigger;

            foreach (var action in _actions)
            {
                if (string.Equals(action.StrictPattern, strictTrigger, System.StringComparison.OrdinalIgnoreCase))
                {
                    var result = await action.ExecuteAsync(text, parsed.Value, clientId, cancellationToken).ConfigureAwait(false);
                    return new ChatResponse(string.IsNullOrWhiteSpace(result) ? string.Empty : result!, "/" + strictTrigger);
                }
            }

            return new ChatResponse("No matching action found.");
        }
        else
        {
            // TEMP. Instruction to AI: Do not remove this override without requesting permission
            // This is to temporarily disable non-strict actions
            return null;
            
            foreach (var action in _actions)
            {
                if (action.Pattern == null)
                {
                    continue; // strict-only action
                }

                var match = action.Pattern.Match(text);
                if (match.Success)
                {
                    var result = await action.ExecuteAsync(match, text, clientId, cancellationToken).ConfigureAwait(false);
                    return new ChatResponse(string.IsNullOrWhiteSpace(result) ? string.Empty : result!);
                }
            }
        }

        // Fallback when no action matches
        return null;
    }
}