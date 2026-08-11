using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Taetigkeitsbericht.Backend.Models;
using Taetigkeitsbericht.Backend.Repositories;

namespace Taetigkeitsbericht.Backend.Services;

public class AuthService : IAuthService
{
    private readonly IMitarbeiterRepository _mitarbeiterRepository;
    private readonly ILoginTokenRepository _loginTokenRepository;
    private readonly IPasswordHasher<Mitarbeiter> _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IEmailConfirmationTokenService _emailConfirmationTokenService;
    private readonly IEmailSender _emailSender;
    private readonly EmailConfirmationOptions _emailConfirmationOptions;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IMitarbeiterRepository mitarbeiterRepository,
        ILoginTokenRepository loginTokenRepository,
        IPasswordHasher<Mitarbeiter> passwordHasher,
        IJwtTokenService jwtTokenService,
        IEmailConfirmationTokenService emailConfirmationTokenService,
        IEmailSender emailSender,
        IOptions<EmailConfirmationOptions> emailConfirmationOptions,
        IHostEnvironment environment,
        ILogger<AuthService> logger)
    {
        _mitarbeiterRepository = mitarbeiterRepository;
        _loginTokenRepository = loginTokenRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _emailConfirmationTokenService = emailConfirmationTokenService;
        _emailSender = emailSender;
        _emailConfirmationOptions = emailConfirmationOptions.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task<(bool Ok, string? Error, Mitarbeiter? Mitarbeiter, string? ConfirmationLink)> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Benutzername)
            || string.IsNullOrWhiteSpace(request.Passwort)
            || string.IsNullOrWhiteSpace(request.Email))
        {
            return (false, "Benutzername, Passwort und E-Mail sind Pflichtfelder.", null, null);
        }

        var benutzername = request.Benutzername.Trim();
        var email = request.Email.Trim();

        var existingByName = await _mitarbeiterRepository.GetByBenutzernameAsync(benutzername, cancellationToken);
        var existingByEmail = await _mitarbeiterRepository.GetByEmailAsync(email, cancellationToken);

        if (existingByName is not null
            && existingByEmail is not null
            && existingByName.Id != existingByEmail.Id)
        {
            return (false, "Benutzername oder E-Mail ist bereits vergeben.", null, null);
        }

        var existing = existingByName ?? existingByEmail;
        if (existing is not null)
        {
            if (existing.EmailBestaetigt)
            {
                return (false, "Benutzername oder E-Mail ist bereits vergeben.", null, null);
            }

            // Noch unbestätigt: neuen Token ausstellen und erneut an die gespeicherte E-Mail senden.
            var resendToken = _emailConfirmationTokenService.CreateToken();
            existing.EmailBestaetigungsToken = resendToken;
            existing.EmailBestaetigungsTokenAblauf = _emailConfirmationTokenService.CreateExpiryUtc();

            // Passwort nur aktualisieren, wenn Benutzername und E-Mail zum bestehenden Konto passen.
            var sameAccount =
                string.Equals(existing.Benutzername, benutzername, StringComparison.Ordinal)
                && string.Equals(existing.Email, email, StringComparison.OrdinalIgnoreCase);
            if (sameAccount)
            {
                existing.PasswortHash = _passwordHasher.HashPassword(existing, request.Passwort);
            }

            await _mitarbeiterRepository.UpdateAsync(existing, cancellationToken);

            var resendLink = BuildConfirmationLink(resendToken);
            await SendConfirmationEmailAsync(existing, resendLink, cancellationToken);

            var resendLinkForClient = _environment.IsDevelopment() ? resendLink : null;
            return (true, null, existing, resendLinkForClient);
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

        var link = BuildConfirmationLink(token);
        await SendConfirmationEmailAsync(mitarbeiter, link, cancellationToken);

        // In Development den Link auch in der API-Antwort liefern (SMTP oft noch aus).
        var linkForClient = _environment.IsDevelopment() ? link : null;
        return (true, null, mitarbeiter, linkForClient);
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

        // Vorherige aktive Sessions desselben Mitarbeiters widerrufen (ein aktuelles Token).
        await _loginTokenRepository.RevokeActiveForMitarbeiterAsync(mitarbeiter.Id, cancellationToken);

        await _loginTokenRepository.AddAsync(
            new LoginToken
            {
                MitarbeiterId = mitarbeiter.Id,
                Jti = jwt.Jti,
                TokenHash = jwt.TokenHash,
                ErstelltAm = DateTimeOffset.UtcNow,
                LaeuftAbAm = jwt.ExpiresAt,
            },
            cancellationToken);

        return (true, null, new LoginResponse
        {
            Token = jwt.Token,
            ExpiresAt = jwt.ExpiresAt,
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

    private string BuildConfirmationLink(string token)
    {
        var baseUrl = _emailConfirmationOptions.ConfirmationBaseUrl.TrimEnd('/');
        return $"{baseUrl}/api/auth/confirm-email?token={Uri.EscapeDataString(token)}";
    }

    private async Task SendConfirmationEmailAsync(
        Mitarbeiter mitarbeiter,
        string link,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "E-Mail-Bestätigungslink für {Benutzername} ({Email}): {ConfirmationLink}",
            mitarbeiter.Benutzername,
            mitarbeiter.Email,
            link);

        var subject = "Bitte E-Mail-Adresse bestätigen";
        var body =
            $"Hallo {mitarbeiter.Benutzername},\n\n" +
            "bitte bestätigen Sie Ihre E-Mail-Adresse für den Tätigkeitsbericht:\n\n" +
            $"{link}\n\n" +
            "Falls Sie sich erneut registriert haben, gilt nur dieser aktuelle Link.\n" +
            "Der Link ist zeitlich begrenzt gültig.\n";

        await _emailSender.SendAsync(mitarbeiter.Email, subject, body, cancellationToken);
    }
}
