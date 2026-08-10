namespace Taetigkeitsbericht.Backend.Models;

/// <summary>
/// Eingabe für den Login (Benutzername und Passwort).
/// Noch ohne Auth-Logik.
/// </summary>
public class LoginRequest
{
    public string Benutzername { get; set; } = string.Empty;

    public string Passwort { get; set; } = string.Empty;
}
