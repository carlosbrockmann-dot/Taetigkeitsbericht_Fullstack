using Microsoft.EntityFrameworkCore;
using Taetigkeitsbericht.Backend.Data;
using Taetigkeitsbericht.Backend.Models;

namespace Taetigkeitsbericht.Backend.Repositories;

public class ZeiteintragRepository : IZeiteintragRepository
{
    private readonly AppDbContext _db;

    public ZeiteintragRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Zeiteintrag>> AddRangeAsync(
        IEnumerable<Zeiteintrag> eintraege,
        CancellationToken cancellationToken = default)
    {
        var liste = eintraege.ToList();
        foreach (var eintrag in liste)
        {
            if (eintrag.Id == Guid.Empty)
            {
                eintrag.Id = Guid.NewGuid();
            }
        }

        _db.Zeiteintraege.AddRange(liste);
        await _db.SaveChangesAsync(cancellationToken);
        return liste;
    }

    public async Task<IReadOnlyList<Zeiteintrag>> GetByMitarbeiterUndZeitraumAsync(
        int mitarbeiterId,
        DateOnly? von,
        DateOnly? bis,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Zeiteintraege
            .AsNoTracking()
            .Where(z => z.MitarbeiterId == mitarbeiterId);

        if (von is not null)
        {
            query = query.Where(z => z.Datum >= von);
        }

        if (bis is not null)
        {
            query = query.Where(z => z.Datum <= bis);
        }

        return await query
            .OrderBy(z => z.Datum)
            .ThenBy(z => z.UhrzeitVon)
            .ToListAsync(cancellationToken);
    }
}
