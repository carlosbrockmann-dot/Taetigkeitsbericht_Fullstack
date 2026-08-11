using Taetigkeitsbericht.Backend.Models;

namespace Taetigkeitsbericht.Backend.Services;

/// <summary>JWT-Ausstellung getrennt von Login-/Registrierungslogik (SRP).</summary>
public interface IJwtTokenService
{
    JwtCreateResult CreateToken(Mitarbeiter mitarbeiter);
}

public sealed record JwtCreateResult(
    string Token,
    string Jti,
    DateTimeOffset ExpiresAt,
    string TokenHash);
