using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CoreServer.Services;

public interface ITokenService
{
    string GenerateToken(string userId, string? email, string displayName, TimeSpan? lifetime = null);
    ClaimsPrincipal? ValidateToken(string token, bool validateLifetime = true);
}

public class TokenService : ITokenService
{
    private readonly IConfiguration _config;
    private readonly JwtSecurityTokenHandler _handler = new();
    private readonly SigningCredentials _creds;
    private readonly TokenValidationParameters _validationParams;

    public TokenService(IConfiguration config)
    {
        _config = config;
        var key = _config["Jwt:Key"] ?? "dev-very-secret-key-change-me-please"; // dev fallback
        var issuer = _config["Jwt:Issuer"] ?? "CoreServer";
        var audience = _config["Jwt:Audience"] ?? "CoreServerClients";
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        _creds = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        _validationParams = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = securityKey,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    }

    public string GenerateToken(string userId, string? email, string displayName, TimeSpan? lifetime = null)
    {
        var issuer = _config["Jwt:Issuer"] ?? "CoreServer";
        var audience = _config["Jwt:Audience"] ?? "CoreServerClients";
        var expires = DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromHours(12));
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new("name", displayName)
        };
        if (!string.IsNullOrWhiteSpace(email))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, email!));
        }
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: _creds
        );
        return _handler.WriteToken(token);
    }

    public ClaimsPrincipal? ValidateToken(string token, bool validateLifetime = true)
    {
        try
        {
            var p = _validationParams.Clone();
            p.ValidateLifetime = validateLifetime;
            return _handler.ValidateToken(token, p, out _);
        }
        catch
        {
            return null;
        }
    }
}
