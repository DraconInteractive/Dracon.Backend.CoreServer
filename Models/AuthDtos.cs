namespace CoreServer.Models;

public record RegisterRequest(string DisplayName, string Password, string? Email);
public record LoginRequest(string DisplayName, string Password);
