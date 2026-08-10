using Microsoft.EntityFrameworkCore;
using Taetigkeitsbericht.Backend.Data;
using Taetigkeitsbericht.Backend.Models;

namespace Taetigkeitsbericht.Backend.Repositories;

public class MitarbeiterRepository : IMitarbeiterRepository
{
    private readonly AppDbContext _db;

    public MitarbeiterRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Mitarbeiter?> GetByBenutzernameAsync(string benutzername, CancellationToken cancellationToken = default)
    {
        return _db.Mitarbeiter
            .FirstOrDefaultAsync(m => m.Benutzername == benutzername, cancellationToken);
    }

    public Task<Mitarbeiter?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return _db.Mitarbeiter
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public Task<Mitarbeiter?> GetByEmailBestaetigungsTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        return _db.Mitarbeiter
            .FirstOrDefaultAsync(m => m.EmailBestaetigungsToken == token, cancellationToken);
    }

    public Task<bool> ExistsBenutzernameOrEmailAsync(string benutzername, string email, CancellationToken cancellationToken = default)
    {
        return _db.Mitarbeiter.AnyAsync(
            m => m.Benutzername == benutzername || m.Email == email,
            cancellationToken);
    }

    public async Task<Mitarbeiter> AddAsync(Mitarbeiter mitarbeiter, CancellationToken cancellationToken = default)
    {
        _db.Mitarbeiter.Add(mitarbeiter);
        await _db.SaveChangesAsync(cancellationToken);
        return mitarbeiter;
    }

    public async Task UpdateAsync(Mitarbeiter mitarbeiter, CancellationToken cancellationToken = default)
    {
        _db.Mitarbeiter.Update(mitarbeiter);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
