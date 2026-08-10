using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Taetigkeitsbericht.Backend.Models;
using Taetigkeitsbericht.Backend.Repositories;

namespace Taetigkeitsbericht.Backend.Services;

public class AuthService : IAuthService
{
    private readonly IMitarbeiterRepository _mitarbeiterRepository;
    private readonly IPasswordHasher<Mitarbeiter> _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IEmailConfirmationTokenService _emailConfirmationTokenService;
    private readonly IEmailSender _emailSender;
    private readonly EmailConfirmationOptions _emailConfirmationOptions;

    public AuthService(
        IMitarbeiterRepository mitarbeiterRepository,
        IPasswordHasher<Mitarbeiter> passwordHasher,
        IJwtTokenService jwtTokenService,
        IEmailConfirmationTokenService emailConfirmationTokenService,
        IEmailSender emailSender,
        IOptions<EmailConfirmationOptions> emailConfirmationOptions)
    {
        _mitarbeiterRepository = mitarbeiterRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _emailConfirmationTokenService = emailConfirmationTokenService;
        _emailSender = emailSender;
        _emailConfirmationOptions = emailConfirmationOptions.Value;
    }

    public async Task<(bool Ok, string? Error, Mitarbeiter? Mitarbeiter)> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Benutzername)
            || string.IsNullOrWhiteSpace(request.Passwort)
            || string.IsNullOrWhiteSpace(request.Email))
        {
            return (false, "Benutzername, Passwort und E-Mail sind Pflichtfelder.", null);
        }

        var benutzername = request.Benutzername.Trim();
        var email = request.Email.Trim();

        if (await _mitarbeiterRepository.ExistsBenutzernameOrEmailAsync(benutzername, email, cancellationToken))
        {
            return (false, "Benutzername oder E-Mail ist bereits vergeben.", null);
        }

        var token = _emailConfirmationTokenService.CreateToken();
        var mitarbeiter = new Mitarbeiter
        {
            Benutzername = benutzername,
            Email = email,
            EmailBestaetigt = false,
            EmailBestaetigungsToken = token,
            EmailBestaetigungsTokenAblauf = _emailConfirmationTokenService.CreateExpiryUtc(),
        };
        mitarbeiter.PasswortHash = _passwordHasher.HashPassword(mitarbeiter, request.Passwort);

        await _mitarbeiterRepository.AddAsync(mitarbeiter, cancellationToken);
        await SendConfirmationEmailAsync(mitarbeiter, token, cancellationToken);

        return (true, null, mitarbeiter);
    }

    public async Task<(bool Ok, string? Error, LoginResponse? Response)> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var mitarbeiter = await _mitarbeiterRepository.GetByBenutzernameAsync(
            request.Benutzername.Trim(),
            cancellationToken);

        if (mitarbeiter is null)
        {
            return (false, "Benutzername oder Passwort ungültig.", null);
        }

        var result = _passwordHasher.VerifyHashedPassword(
            mitarbeiter,
            mitarbeiter.PasswortHash,
            request.Passwort);

        if (result == PasswordVerificationResult.Failed)
        {
            return (false, "Benutzername oder Passwort ungültig.", null);
        }

        if (!mitarbeiter.EmailBestaetigt)
        {
            return (false, "Bitte zuerst die E-Mail-Adresse bestätigen.", null);
        }

        var jwt = _jwtTokenService.CreateToken(mitarbeiter);
        return (true, null, new LoginResponse
        {
            Token = jwt,
            MitarbeiterId = mitarbeiter.Id,
            Benutzername = mitarbeiter.Benutzername,
        });
    }

    public async Task<(bool Ok, string? Error)> ConfirmEmailAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return (false, "Bestätigungstoken fehlt.");
        }

        var mitarbeiter = await _mitarbeiterRepository.GetByEmailBestaetigungsTokenAsync(
            token.Trim(),
            cancellationToken);

        if (mitarbeiter is null)
        {
            return (false, "Ungültiger Bestätigungstoken.");
        }

        if (mitarbeiter.EmailBestaetigt)
        {
            return (true, null);
        }

        if (!_emailConfirmationTokenService.IsValid(
                mitarbeiter.EmailBestaetigungsToken,
                mitarbeiter.EmailBestaetigungsTokenAblauf,
                token.Trim()))
        {
            return (false, "Bestätigungstoken ist ungültig oder abgelaufen.");
        }

        mitarbeiter.EmailBestaetigt = true;
        mitarbeiter.EmailBestaetigungsToken = null;
        mitarbeiter.EmailBestaetigungsTokenAblauf = null;
        await _mitarbeiterRepository.UpdateAsync(mitarbeiter, cancellationToken);

        return (true, null);
    }

    private async Task SendConfirmationEmailAsync(
        Mitarbeiter mitarbeiter,
        string token,
        CancellationToken cancellationToken)
    {
        var baseUrl = _emailConfirmationOptions.ConfirmationBaseUrl.TrimEnd('/');
        var link = $"{baseUrl}/api/auth/confirm-email?token={Uri.EscapeDataString(token)}";
        var subject = "Bitte E-Mail-Adresse bestätigen";
        var body =
            $"Hallo {mitarbeiter.Benutzername},\n\n" +
            "bitte bestätigen Sie Ihre E-Mail-Adresse für den Tätigkeitsbericht:\n\n" +
            $"{link}\n\n" +
            "Der Link ist zeitlich begrenzt gültig.\n";

        await _emailSender.SendAsync(mitarbeiter.Email, subject, body, cancellationToken);
    }
}
