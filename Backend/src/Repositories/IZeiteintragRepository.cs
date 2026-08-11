using Taetigkeitsbericht.Backend.Models;

namespace Taetigkeitsbericht.Backend.Repositories;

public interface IZeiteintragRepository
{
    Task<IReadOnlyList<Zeiteintrag>> AddRangeAsync(
        IEnumerable<Zeiteintrag> eintraege,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Löscht die Zeiteinträge nur für diesen Mitarbeiter, Mandant und Kalendermonat
    /// und fügt anschließend die neuen Einträge ein.
    /// </summary>
    Task<IReadOnlyList<Zeiteintrag>> ReplaceMonatAsync(
        int mitarbeiterId,
        int? mandantId,
        int jahr,
        int monat,
        IEnumerable<Zeiteintrag> eintraege,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Zeiteintrag>> GetByMitarbeiterUndZeitraumAsync(
        int mitarbeiterId,
        DateOnly? von,
        DateOnly? bis,
        int? mandantId = null,
        CancellationToken cancellationToken = default);
}
