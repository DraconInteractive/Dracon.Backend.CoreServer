using System;

namespace CoreServer.Models;

public class ChatMessage
{
    public enum MessageType
    {
        Message,
        Event
    }
    
    public string ClientId { get; set; } = string.Empty;
    public MessageType Type { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTimeOffset TS { get; set; }
}