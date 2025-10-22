namespace CoreServer.Models;

public class ChatResponse
{
    public string ResponseText;
    public string? EchoOverride = null;
    
    public ChatResponse(string text, string? echoOverride = null)
    {
        ResponseText = text;
        EchoOverride = echoOverride;
    }
}