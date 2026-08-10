namespace Taetigkeitsbericht.Backend.Services;

/// <summary>Versendet E-Mails (DIP: austauschbare Implementierung).</summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default);
}
