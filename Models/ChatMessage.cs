using System;

namespace CoreServer.Models;

public class ChatMessage
{
    public string sender { get; set; } = string.Empty;
    public string id { get; set; } = string.Empty;
    public string text { get; set; } = string.Empty;
    public DateTimeOffset ts { get; set; }
}