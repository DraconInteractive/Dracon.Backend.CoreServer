using System;

namespace CoreServer.Models;

public class ChatMessage
{
    public string Sender { get; set; } = "system";
    public string Text { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
