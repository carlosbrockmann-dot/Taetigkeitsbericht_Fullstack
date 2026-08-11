using Taetigkeitsbericht.Backend.Models;

namespace Taetigkeitsbericht.Backend.Services;

/// <summary>
/// Regeln für die Monatsabgabe: mehrere Arbeitseinträge mit unterschiedlichen
/// Uhrzeiten je Tag/Mandant; Urlaub/Krankheit höchstens einmal je Tag/Mandant.
/// </summary>
public static class ZeiteintragAbgabeValidator
{
    public static string? Validiere(IReadOnlyList<Zeiteintrag> eintraege)
    {
        foreach (var gruppe in eintraege.GroupBy(e => (e.Datum, e.MandantId)))
        {
            var urlaubKrank = gruppe
                .Where(e =>
                    e.Kategorie is ZeiteintragKategorie.Urlaub
                        or ZeiteintragKategorie.Krankheit
                        or ZeiteintragKategorie.Sonderurlaub)
                .ToList();
            if (urlaubKrank.Count > 1)
            {
                var mandant = gruppe.Key.MandantId?.ToString() ?? "–";
                return $"Für {gruppe.Key.Datum:yyyy-MM-dd} (Mandant {mandant}) "
                    + "darf Urlaub/Krankheit nur einmal abgegeben werden.";
            }

            var arbeit = gruppe
                .Where(e => e.Kategorie == ZeiteintragKategorie.Arbeitstag)
                .ToList();
            foreach (var eintrag in arbeit)
            {
                if (eintrag.UhrzeitVon is null || eintrag.UhrzeitBis is null)
                {
                    return $"Arbeitstag {eintrag.Datum:yyyy-MM-dd} braucht UhrzeitVon und UhrzeitBis.";
                }
            }

            var doppelteZeiten = arbeit
                .GroupBy(e => (e.UhrzeitVon, e.UhrzeitBis))
                .FirstOrDefault(g => g.Count() > 1);
            if (doppelteZeiten is not null)
            {
                var (von, bis) = doppelteZeiten.Key;
                var mandant = gruppe.Key.MandantId?.ToString() ?? "–";
                return $"Für {gruppe.Key.Datum:yyyy-MM-dd} (Mandant {mandant}) "
                    + $"gibt es doppelte Arbeitszeiten ({von:HH':'mm}–{bis:HH':'mm}).";
            }

            if (urlaubKrank.Count > 0 && arbeit.Count > 0)
            {
                var mandant = gruppe.Key.MandantId?.ToString() ?? "–";
                return $"Für {gruppe.Key.Datum:yyyy-MM-dd} (Mandant {mandant}) "
                    + "sind Arbeitseinträge und Urlaub/Krankheit gleichzeitig nicht erlaubt.";
            }
        }

        return null;
    }
}
