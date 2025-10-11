using System.Collections.Concurrent;
using CoreServer.Models;

namespace CoreServer.Services;

public class InMemoryUserRegistry : IUserRegistry
{
    private readonly ConcurrentDictionary<string, User> _users = new();

    public User AddOrUpdate(string id, string name)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            id = Guid.NewGuid().ToString("N");
        }

        var user = _users.AddOrUpdate(id,
            _ => new User { Id = id, Name = name },
            (_, existing) =>
            {
                existing.Name = name;
                return existing;
            });
        return user;
    }

    public User? GetById(string id)
    {
        return _users.TryGetValue(id, out var user) ? user : null;
    }

    public IReadOnlyCollection<User> GetAll()
    {
        return _users.Values.ToArray();
    }
}
