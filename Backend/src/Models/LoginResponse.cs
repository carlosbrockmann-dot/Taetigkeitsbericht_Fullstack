namespace Taetigkeitsbericht.Backend.Models;

/// <summary>
/// Ergebnis eines erfolgreichen Logins (Token und Mitarbeiter-Bezug).
/// Noch ohne Token-Ausstellung.
/// </summary>
public class LoginResponse
{
    public string Token { get; set; } = string.Empty;

    public int MitarbeiterId { get; set; }

    public string Benutzername { get; set; } = string.Empty;
}
