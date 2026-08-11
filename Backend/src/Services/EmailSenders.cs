using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Taetigkeitsbericht.Backend.Services;

/// <summary>
/// Entwicklungs-Implementierung: Log + Datei unter logs/last-confirmation-email.txt.
/// Kein echter SMTP-Versand.
/// </summary>
public class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;
    private readonly IHostEnvironment _environment;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "SMTP ist deaktiviert – E-Mail wird NICHT zugestellt. Inhalt:\nAn: {ToEmail}\nBetreff: {Subject}\n{Body}",
            toEmail,
            subject,
            body);

        var logsDir = System.IO.Path.Combine(_environment.ContentRootPath, "logs");
        Directory.CreateDirectory(logsDir);
        var path = System.IO.Path.Combine(logsDir, "last-confirmation-email.txt");
        var content =
            $"Zeit (UTC): {DateTime.UtcNow:O}\n" +
            $"An: {toEmail}\n" +
            $"Betreff: {subject}\n\n" +
            $"{body}\n";
        await File.WriteAllTextAsync(path, content, cancellationToken);
        _logger.LogWarning("Bestätigungslink auch gespeichert unter: {EmailFilePath}", path);
    }
}

public class SmtpEmailOptions
{
    public const string SectionName = "Smtp";

    public bool Enabled { get; set; }

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 587;

    public string From { get; set; } = "noreply@example.com";

    public string? UserName { get; set; }

    public string? Password { get; set; }

    public bool EnableSsl { get; set; } = true;
}

/// <summary>Echter SMTP-Versand über MailKit (z. B. GMX: mail.gmx.net:587).</summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpEmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpEmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Host)
            || string.IsNullOrWhiteSpace(_options.From)
            || string.IsNullOrWhiteSpace(_options.UserName)
            || string.IsNullOrWhiteSpace(_options.Password))
        {
            throw new InvalidOperationException(
                "SMTP ist aktiviert, aber Host/From/UserName/Password sind nicht vollständig konfiguriert. " +
                "Siehe README (User Secrets).");
        }

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_options.From));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        try
        {
            using var client = new SmtpClient();
            var secureSocket = _options.EnableSsl
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.None;

            await client.ConnectAsync(_options.Host, _options.Port, secureSocket, cancellationToken);
            await client.AuthenticateAsync(_options.UserName, _options.Password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Bestätigungs-E-Mail per SMTP an {ToEmail} gesendet.", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "SMTP-Versand an {ToEmail} fehlgeschlagen (Host={Host}:{Port}).",
                toEmail,
                _options.Host,
                _options.Port);
            throw new InvalidOperationException(
                $"E-Mail konnte nicht gesendet werden: {ex.Message}",
                ex);
        }
    }
}
