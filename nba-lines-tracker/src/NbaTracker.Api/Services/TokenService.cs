using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using NbaTracker.Data.Entities;

namespace NbaTracker.Api.Services;

public class TokenService
{
    private readonly SymmetricSecurityKey _key;

    public TokenService(IConfiguration config)
    {
        // ASP.NET Core maps env var JWT__Secret -> config key JWT:Secret via __ separator
        var secret = config["JWT:Secret"]
            ?? throw new InvalidOperationException("JWT__Secret env var must be configured");
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
    }

    public string GenerateAccessToken(User user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.IsAdmin ? "Admin" : "User"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(15),
            Issuer = "nbatracker-api",
            Audience = "nbatracker-client",
            SigningCredentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256)
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    // Returns cryptographically random 64-byte plaintext refresh token (Base64 encoded)
    public string GenerateRefreshToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    // Store only the BCrypt hash — plaintext is returned to the client once and never persisted
    public string HashRefreshToken(string plaintext)
        => BCrypt.Net.BCrypt.HashPassword(plaintext);

    public bool VerifyRefreshToken(string plaintext, string hash)
        => BCrypt.Net.BCrypt.Verify(plaintext, hash);
}
