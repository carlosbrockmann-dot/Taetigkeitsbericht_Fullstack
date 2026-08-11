using HotChocolate.Authorization;
using Microsoft.AspNetCore.Http;
using Taetigkeitsbericht.Backend.Models;
using Taetigkeitsbericht.Backend.Repositories;
using Taetigkeitsbericht.Backend.Services;

namespace Taetigkeitsbericht.Backend.GraphQL;

public class Mutation
{
    public async Task<RegisterPayload> RegisterAsync(
        RegisterRequest input,
        [Service] IAuthService authService,
        [Service] Microsoft.Extensions.Options.IOptions<SmtpEmailOptions> smtpOptions,
        CancellationToken cancellationToken)
    {
        var (ok, error, mitarbeiter, confirmationLink) = await authService.RegisterAsync(input, cancellationToken);
        if (!ok || mitarbeiter is null)
        {
            return new RegisterPayload { Ok = false, Error = error };
        }

        var hinweis = smtpOptions.Value.Enabled
            ? "Bitte Posteingang (und Spam) der registrierten E-Mail prüfen – Bestätigungslink wurde gesendet "
              + "(auch bei erneuter Registrierung eines noch unbestätigten Kontos)."
            : confirmationLink is null
                ? "Bitte E-Mail-Adresse über den Bestätigungslink bestätigen."
                : "Development: SMTP ist aus – Bestätigungslink unten bzw. logs/last-confirmation-email.txt.";

        return new RegisterPayload
        {
            Ok = true,
            MitarbeiterId = mitarbeiter.Id,
            Benutzername = mitarbeiter.Benutzername,
            Email = mitarbeiter.Email,
            EmailBestaetigt = mitarbeiter.EmailBestaetigt,
            Hinweis = hinweis,
            ConfirmationLink = confirmationLink,
        };
    }

    public async Task<LoginPayload> LoginAsync(
        LoginRequest input,
        [Service] IAuthService authService,
        CancellationToken cancellationToken)
    {
        var (ok, error, response) = await authService.LoginAsync(input, cancellationToken);
        if (!ok || response is null)
        {
            return new LoginPayload { Ok = false, Error = error };
        }

        return new LoginPayload { Ok = true, Login = response };
    }

    public async Task<ConfirmEmailPayload> ConfirmEmailAsync(
        string token,
        [Service] IAuthService authService,
        CancellationToken cancellationToken)
    {
        var (ok, error) = await authService.ConfirmEmailAsync(token, cancellationToken);
        if (!ok)
        {
            return new ConfirmEmailPayload { Ok = false, Error = error };
        }

        return new ConfirmEmailPayload
        {
            Ok = true,
            Message = "E-Mail-Adresse erfolgreich bestätigt. Sie können sich jetzt anmelden.",
        };
    }

    [Authorize]
    public async Task<SpeichereZeiteintraegePayload> SpeichereZeiteintraegeAsync(
        List<ZeiteintragInput> eintraege,
        [Service] IZeiteintragRepository repository,
        [Service] ICurrentUserService currentUser,
        [Service] IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken)
    {
        var user = httpContextAccessor.HttpContext?.User;
        var mitarbeiterId = user is null ? null : currentUser.GetMitarbeiterId(user);
        if (mitarbeiterId is null)
        {
            return new SpeichereZeiteintraegePayload { Ok = false, Error = "Nicht authentifiziert." };
        }

        if (eintraege.Count == 0)
        {
            return new SpeichereZeiteintraegePayload
            {
                Ok = false,
                Error = "Liste der Zeiteinträge ist leer.",
            };
        }

        var entities = eintraege.Select(e => e.ToEntity(mitarbeiterId.Value)).ToList();
        var gespeichert = await repository.AddRangeAsync(entities, cancellationToken);

        return new SpeichereZeiteintraegePayload { Ok = true, Eintraege = gespeichert };
    }
}
