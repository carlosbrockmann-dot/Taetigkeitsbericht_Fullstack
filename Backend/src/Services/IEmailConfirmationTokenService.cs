namespace Taetigkeitsbericht.Backend.Services;

/// <summary>Erzeugt und prüft Tokens zur E-Mail-Bestätigung (SRP).</summary>
public interface IEmailConfirmationTokenService
{
    string CreateToken();

    DateTimeOffset CreateExpiryUtc();

    bool IsValid(string? storedToken, DateTimeOffset? expiryUtc, string providedToken);
}
