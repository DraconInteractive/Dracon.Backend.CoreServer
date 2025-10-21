using CoreServer.Services;

namespace CoreServer.Logic;

public class RestApiService : IRestApiService
{
    private readonly IUserRegistry _users;

    public RestApiService(IUserRegistry users)
    {
        _users = users;
    }

    public string OnlinePing() => "pong";

    public int GetLatestVersion() => 1;

    public void RegisterUser(string? id, string? name)
    {
        
    }

    public void RegisterDevice(string? id, string? name, string? mac)
    {
        
    }
}
