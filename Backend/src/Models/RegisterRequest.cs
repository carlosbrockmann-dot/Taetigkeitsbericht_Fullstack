namespace Taetigkeitsbericht.Backend.Models;

/// <summary>
/// Eingabe zur Registrierung eines neuen Mitarbeiters.
/// </summary>
public class RegisterRequest
{
    public string Benutzername { get; set; } = string.Empty;

    public string Passwort { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
}
