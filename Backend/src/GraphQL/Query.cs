using System.Security.Claims;
using HotChocolate.Authorization;
using Microsoft.AspNetCore.Http;
using Taetigkeitsbericht.Backend.Models;
using Taetigkeitsbericht.Backend.Repositories;
using Taetigkeitsbericht.Backend.Services;

namespace Taetigkeitsbericht.Backend.GraphQL;

public class Query
{
    [Authorize]
    public async Task<IReadOnlyList<Zeiteintrag>> ZeiteintraegeAsync(
        DateOnly? von,
        DateOnly? bis,
        [Service] IZeiteintragRepository repository,
        [Service] ICurrentUserService currentUser,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken)
    {
        var user = httpContextAccessor.HttpContext?.User
            ?? throw new GraphQLException("Nicht authentifiziert.");

        var mitarbeiterId = currentUser.GetMitarbeiterId(user)
            ?? throw new GraphQLException("Nicht authentifiziert.");

        return await repository.GetByMitarbeiterUndZeitraumAsync(
            mitarbeiterId,
            von,
            bis,
            cancellationToken);
    }
}
