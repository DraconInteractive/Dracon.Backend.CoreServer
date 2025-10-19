using System;

namespace CoreServer.Models;

public class ChatMessage
{
    public enum MessageType
    {
        Message,
        Event
    }
    
    public string senderId { get; set; } = string.Empty;
    public MessageType type { get; set; }
    public string text { get; set; } = string.Empty;
    public DateTimeOffset ts { get; set; }
}