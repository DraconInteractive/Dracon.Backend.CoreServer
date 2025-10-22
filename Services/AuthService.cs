using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace CoreServer.Services;

public interface IAuthService
{
    Task InitializeAsync(CancellationToken ct = default);
    Task<(bool ok, string? error)> RegisterAsync(string displayName, string password, string? email, CancellationToken ct = default);
    Task<(bool ok, string? userId, string? email, string? displayName, string? error)> LoginAsync(string displayName, string password, CancellationToken ct = default);
}

public class AuthService : IAuthService
{
    private readonly string? _connStr;

    public AuthService(IConfiguration config)
    {
        _connStr = config.GetConnectionString("Default");
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_connStr)) return; // no DB configured
        await using var conn = new SqlConnection(_connStr);
        await conn.OpenAsync(ct);
        var cmdText = @"
-- Create table with target schema if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.Users (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        DisplayName NVARCHAR(256) NOT NULL,
        Email NVARCHAR(256) NULL,
        PasswordHash VARBINARY(512) NOT NULL,
        PasswordSalt VARBINARY(128) NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
    CREATE UNIQUE INDEX IX_Users_DisplayName ON dbo.Users(DisplayName);
END";
        await using var cmd = new SqlCommand(cmdText, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<(bool ok, string? error)> RegisterAsync(string displayName, string password, string? email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_connStr)) return (false, "Database not configured");
        var (hash, salt) = HashPassword(password);
        await using var conn = new SqlConnection(_connStr);
        await conn.OpenAsync(ct);
        var cmdText = @"IF EXISTS (SELECT 1 FROM dbo.Users WHERE DisplayName = @name)
    SELECT CAST(1 AS INT);
ELSE
BEGIN
    INSERT INTO dbo.Users (DisplayName, Email, PasswordHash, PasswordSalt)
    VALUES (@name, @email, @hash, @salt);
    SELECT CAST(0 AS INT);
END";
        await using var cmd = new SqlCommand(cmdText, conn);
        cmd.Parameters.AddWithValue("@name", displayName);
        cmd.Parameters.AddWithValue("@email", (object?)email ?? DBNull.Value);
        cmd.Parameters.Add("@hash", SqlDbType.VarBinary, hash.Length).Value = hash;
        cmd.Parameters.Add("@salt", SqlDbType.VarBinary, salt.Length).Value = salt;
        var exists = (int)(await cmd.ExecuteScalarAsync(ct) ?? 0);
        if (exists == 1) return (false, "Display name already exists");
        return (true, null);
    }

    public async Task<(bool ok, string? userId, string? email, string? displayName, string? error)> LoginAsync(string displayName, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_connStr)) return (false, null, null, null, "Database not configured");
        await using var conn = new SqlConnection(_connStr);
        await conn.OpenAsync(ct);
        var cmdText = "SELECT TOP 1 Id, Email, DisplayName, PasswordHash, PasswordSalt FROM dbo.Users WHERE DisplayName = @name";
        await using var cmd = new SqlCommand(cmdText, conn);
        cmd.Parameters.AddWithValue("@name", displayName);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return (false, null, null, null, "Invalid credentials");
        var id = reader.GetGuid(0).ToString();
        var realEmail = reader.IsDBNull(1) ? null : reader.GetString(1);
        var name = reader.GetString(2);
        var hash = (byte[])reader[3];
        var salt = (byte[])reader[4];
        if (!VerifyPassword(password, hash, salt)) return (false, null, null, null, "Invalid credentials");
        return (true, id, realEmail, name, null);
    }

    private static (byte[] hash, byte[] salt) HashPassword(string password)
    {
        using var rng = RandomNumberGenerator.Create();
        var salt = new byte[16];
        rng.GetBytes(salt);
        using var derive = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
        var hash = derive.GetBytes(32);
        return (hash, salt);
    }

    private static bool VerifyPassword(string password, byte[] storedHash, byte[] storedSalt)
    {
        using var derive = new Rfc2898DeriveBytes(password, storedSalt, 100_000, HashAlgorithmName.SHA256);
        var hash = derive.GetBytes(32);
        return CryptographicOperations.FixedTimeEquals(hash, storedHash);
    }
}
