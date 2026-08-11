using Taetigkeitsbericht.Backend.Models;

namespace Taetigkeitsbericht.Backend.Repositories;

public interface IMitarbeiterRepository
{
    Task<Mitarbeiter?> GetByBenutzernameAsync(string benutzername, CancellationToken cancellationToken = default);

    Task<Mitarbeiter?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<Mitarbeiter?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<Mitarbeiter?> GetByEmailBestaetigungsTokenAsync(string token, CancellationToken cancellationToken = default);

    Task<bool> ExistsBenutzernameOrEmailAsync(string benutzername, string email, CancellationToken cancellationToken = default);

    Task<Mitarbeiter> AddAsync(Mitarbeiter mitarbeiter, CancellationToken cancellationToken = default);

    Task UpdateAsync(Mitarbeiter mitarbeiter, CancellationToken cancellationToken = default);
}
