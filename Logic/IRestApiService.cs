namespace CoreServer.Logic;

public interface IRestApiService
{
    string OnlinePing();
    int GetLatestVersion();
    // Register is intentionally a stub for now per requirements.
    void RegisterDevice(string? id, string? name, string? mac);
}
