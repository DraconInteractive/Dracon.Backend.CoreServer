namespace CoreServer.Logic;

public interface IServerLogic
{
    string OnlinePing();
    int GetLatestVersion();
    // Register is intentionally a stub for now per requirements.
    void Register(string? id, string? name);
}
