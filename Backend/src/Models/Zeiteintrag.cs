using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HotChocolate;

namespace Taetigkeitsbericht.Backend.Models;

/// <summary>
/// Zeiteintrag analog zur Desktop-Erfassung, ergänzt um Mitarbeiter-ID und Kategorie
/// (Arbeitstag, Urlaub, Krankheit, Abwesenheit usw.). Uhrzeiten/Pausen sind optional,
/// damit Abwesenheitstage ohne Arbeitszeiten speicherbar sind.
/// </summary>
public class Zeiteintrag
{
    [Key]
    public Guid Id { get; set; }

    public int MitarbeiterId { get; set; }

    [ForeignKey(nameof(MitarbeiterId))]
    [GraphQLIgnore]
    public Mitarbeiter? Mitarbeiter { get; set; }

    /// <summary>Benutzername des Mitarbeiters (nur für Abfragen, nicht persistiert).</summary>
    [NotMapped]
    public string? MitarbeiterName { get; set; }

    public int? MandantId { get; set; }

    public DateOnly Datum { get; set; }

    /// <summary>Arbeitstag, Urlaub, Krankheit, Abwesenheit, Sonderurlaub, …</summary>
    public ZeiteintragKategorie Kategorie { get; set; } = ZeiteintragKategorie.Arbeitstag;

    public TimeOnly? UhrzeitVon { get; set; }

    public TimeOnly? UhrzeitBis { get; set; }

    public TimeOnly? PauseBeginn { get; set; }

    public TimeOnly? PauseEnde { get; set; }

    public TimeOnly? Pause2Beginn { get; set; }

    public TimeOnly? Pause2Ende { get; set; }

    [MaxLength(80)]
    public string? Anmerkung { get; set; }
}
