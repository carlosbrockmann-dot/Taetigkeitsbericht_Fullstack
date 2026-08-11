namespace Taetigkeitsbericht.Backend.Models;

/// <summary>
/// Ergebnis eines erfolgreichen Logins (Token, Ablauf und Mitarbeiter-Bezug).
/// </summary>
public class LoginResponse
{
    public string Token { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public int MitarbeiterId { get; set; }

    public string Benutzername { get; set; } = string.Empty;
}
