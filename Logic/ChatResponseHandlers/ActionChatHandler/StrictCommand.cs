using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace CoreServer.Logic;

public readonly struct StrictCommand
{
    public string Trigger { get; }
    public string[] Args { get; }

    public StrictCommand(string trigger, string[] args)
    {
        Trigger = trigger;
        Args = args;
    }

    public static bool TryParse(string text, [NotNullWhen(true)] out StrictCommand? command)
    {
        command = null;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var span = text.AsSpan().Trim();
        if (span.Length == 0) return false;

        // Leading slash should not be present here; ActionChatResponseHandler already strips it.
        // Do not handle '/' here to avoid double-handling.

        var tokens = Tokenize(span);
        if (tokens.Count == 0) return false;
        var trigger = tokens[0];
        var args = tokens.Count > 1 ? tokens.GetRange(1, tokens.Count - 1).ToArray() : Array.Empty<string>();
        command = new StrictCommand(trigger, args);
        return true;
    }

    private static List<string> Tokenize(ReadOnlySpan<char> input)
    {
        var tokens = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        char quoteChar = '\0';

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (inQuotes)
            {
                if (c == quoteChar)
                {
                    inQuotes = false;
                }
                else if (c == '\\' && i + 1 < input.Length && input[i + 1] == quoteChar)
                {
                    sb.Append(quoteChar);
                    i++; // skip escaped quote
                }
                else
                {
                    sb.Append(c);
                }
            }
            else
            {
                if (char.IsWhiteSpace(c))
                {
                    if (sb.Length > 0)
                    {
                        tokens.Add(sb.ToString());
                        sb.Clear();
                    }
                }
                else if (c == '"' || c == '\'')
                {
                    inQuotes = true;
                    quoteChar = c;
                }
                else
                {
                    sb.Append(c);
                }
            }
        }

        if (sb.Length > 0)
        {
            tokens.Add(sb.ToString());
        }

        return tokens;
    }
}
