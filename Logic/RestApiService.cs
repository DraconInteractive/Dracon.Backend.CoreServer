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

    public void Register(string? id, string? name)
    {
        // Per requirement: Leave method empty for now.
        // This can later be implemented to add/update users via _users.AddOrUpdate(id ?? string.Empty, name ?? string.Empty);
    }
}
