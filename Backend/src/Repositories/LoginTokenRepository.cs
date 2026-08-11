using Microsoft.EntityFrameworkCore;
using Taetigkeitsbericht.Backend.Data;
using Taetigkeitsbericht.Backend.Models;

namespace Taetigkeitsbericht.Backend.Repositories;

public class LoginTokenRepository : ILoginTokenRepository
{
    private readonly AppDbContext _db;

    public LoginTokenRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<LoginToken> AddAsync(LoginToken token, CancellationToken cancellationToken = default)
    {
        _db.LoginTokens.Add(token);
        await _db.SaveChangesAsync(cancellationToken);
        return token;
    }

    public async Task RevokeActiveForMitarbeiterAsync(int mitarbeiterId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var active = await _db.LoginTokens
            .Where(t => t.MitarbeiterId == mitarbeiterId && t.WiderrufenAm == null && t.LaeuftAbAm > now)
            .ToListAsync(cancellationToken);

        foreach (var token in active)
        {
            token.WiderrufenAm = now;
        }

        if (active.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public Task<bool> IsActiveAsync(string jti, string tokenHash, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return _db.LoginTokens.AnyAsync(
            t => t.Jti == jti
                && t.TokenHash == tokenHash
                && t.WiderrufenAm == null
                && t.LaeuftAbAm > now,
            cancellationToken);
    }
}
