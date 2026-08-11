using Taetigkeitsbericht.Backend.Models;

namespace Taetigkeitsbericht.Backend.Repositories;

public interface ILoginTokenRepository
{
    Task<LoginToken> AddAsync(LoginToken token, CancellationToken cancellationToken = default);

    Task RevokeActiveForMitarbeiterAsync(int mitarbeiterId, CancellationToken cancellationToken = default);

    Task<bool> IsActiveAsync(string jti, string tokenHash, CancellationToken cancellationToken = default);
}
