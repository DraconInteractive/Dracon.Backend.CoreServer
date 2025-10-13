using System.Text.Json;

namespace CoreServer.Models;

[Serializable]
public class JsonReturnPacket
{
    public string? ClientId { get; set; }
    public string? Message { get; set; }
    public bool Error { get; set; }

    public JsonReturnPacket(string clientId, string message, bool error)
    {
        ClientId = clientId;
        Message = message;
        Error = error;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        
    };
    
    public string GetJson()
    {
        return JsonSerializer.Serialize((object)this, JsonOptions);
    }
}