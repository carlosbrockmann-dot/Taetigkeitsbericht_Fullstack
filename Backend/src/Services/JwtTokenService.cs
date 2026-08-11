using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Taetigkeitsbericht.Backend.Models;

namespace Taetigkeitsbericht.Backend.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public JwtCreateResult CreateToken(Mitarbeiter mitarbeiter)
    {
        var jwtSection = _configuration.GetSection("Jwt");
        var key = jwtSection["Key"]
            ?? throw new InvalidOperationException("Jwt:Key fehlt in der Konfiguration.");
        var issuer = jwtSection["Issuer"] ?? "Taetigkeitsbericht.Backend";
        var audience = jwtSection["Audience"] ?? "Taetigkeitsbericht";
        var expiresMinutes = int.TryParse(jwtSection["ExpiresMinutes"], out var m) ? m : 60;

        var jti = Guid.NewGuid().ToString("N");
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(expiresMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, mitarbeiter.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, jti),
            new Claim(ClaimTypes.NameIdentifier, mitarbeiter.Id.ToString()),
            new Claim(ClaimTypes.Name, mitarbeiter.Benutzername),
            new Claim(JwtRegisteredClaimNames.UniqueName, mitarbeiter.Benutzername),
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return new JwtCreateResult(tokenString, jti, expiresAt, ComputeTokenHash(tokenString));
    }

    public static string ComputeTokenHash(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }
}
