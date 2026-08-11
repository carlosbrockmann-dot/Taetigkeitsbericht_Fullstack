using Taetigkeitsbericht.Backend.Models;

namespace Taetigkeitsbericht.Backend.Services;

public interface IAuthService
{
    Task<(bool Ok, string? Error, Mitarbeiter? Mitarbeiter, string? ConfirmationLink)> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Ok, string? Error, LoginResponse? Response)> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Ok, string? Error)> ConfirmEmailAsync(
        string token,
        CancellationToken cancellationToken = default);
}
