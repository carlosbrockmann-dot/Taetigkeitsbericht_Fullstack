namespace Taetigkeitsbericht.Backend.Models;

/// <summary>
/// Art des Tages-/Zeiteintrags (Arbeit, Abwesenheit usw.).
/// </summary>
public enum ZeiteintragKategorie
{
    Arbeitstag = 0,
    Urlaub = 1,
    Sonderurlaub = 2,
    Krankheit = 3,
    Abwesenheit = 4,
    Feiertag = 5,
    Betriebsferien = 6,
}
