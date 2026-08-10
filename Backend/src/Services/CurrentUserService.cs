using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Taetigkeitsbericht.Backend.Services;

public interface ICurrentUserService
{
    int? GetMitarbeiterId(ClaimsPrincipal user);
}

public class CurrentUserService : ICurrentUserService
{
    public int? GetMitarbeiterId(ClaimsPrincipal user)
    {
        var idClaim = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return int.TryParse(idClaim, out var mitarbeiterId) ? mitarbeiterId : null;
    }
}
