using Taetigkeitsbericht.Backend.Models;

namespace Taetigkeitsbericht.Backend.Services;

/// <summary>JWT-Ausstellung getrennt von Login-/Registrierungslogik (SRP).</summary>
public interface IJwtTokenService
{
    string CreateToken(Mitarbeiter mitarbeiter);
}
