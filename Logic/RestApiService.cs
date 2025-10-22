using CoreServer.Services;

namespace CoreServer.Logic;

public class RestApiService : IRestApiService
{
    public RestApiService()
    {
    }

    public string OnlinePing() => "pong";

    public int GetLatestVersion() => 1;

    public void RegisterDevice(string? id, string? name, string? mac)
    {
        
    }
}
