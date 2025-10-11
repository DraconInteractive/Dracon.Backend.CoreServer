using CoreServer.Models;

namespace CoreServer.Services;

public interface IUserRegistry
{
    // Adds a new user or updates existing by Id. If id is empty, generates a new one and returns it.
    User AddOrUpdate(string id, string name);
    User? GetById(string id);
    IReadOnlyCollection<User> GetAll();
}
