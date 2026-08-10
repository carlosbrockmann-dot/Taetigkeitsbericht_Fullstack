using Taetigkeitsbericht.Backend.Models;

namespace Taetigkeitsbericht.Backend.Repositories;

public interface IZeiteintragRepository
{
    Task<IReadOnlyList<Zeiteintrag>> AddRangeAsync(
        IEnumerable<Zeiteintrag> eintraege,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Zeiteintrag>> GetByMitarbeiterUndZeitraumAsync(
        int mitarbeiterId,
        DateOnly? von,
        DateOnly? bis,
        CancellationToken cancellationToken = default);
}
