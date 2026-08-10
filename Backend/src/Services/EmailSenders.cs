using Microsoft.Extensions.Options;

namespace Taetigkeitsbericht.Backend.Services;

/// <summary>
/// Entwicklungs-Implementierung: schreibt die E-Mail in den Log (kein SMTP nötig).
/// In Produktion durch echte SMTP-/Provider-Implementierung ersetzen (OCP/DIP).
/// </summary>
public class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "E-Mail an {ToEmail}\nBetreff: {Subject}\n{Body}",
            toEmail,
            subject,
            body);
        return Task.CompletedTask;
    }
}

public class SmtpEmailOptions
{
    public const string SectionName = "Smtp";

    public bool Enabled { get; set; }

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 25;

    public string From { get; set; } = "noreply@taetigkeitsbericht.local";

    public string? UserName { get; set; }

    public string? Password { get; set; }

    public bool EnableSsl { get; set; }
}

/// <summary>SMTP-Versand für On-Premises/Produktion.</summary>
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
        using var message = new System.Net.Mail.MailMessage(_options.From, toEmail, subject, body);
        using var client = new System.Net.Mail.SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
        };

        if (!string.IsNullOrWhiteSpace(_options.UserName))
        {
            client.Credentials = new System.Net.NetworkCredential(_options.UserName, _options.Password);
        }

        await client.SendMailAsync(message, cancellationToken);
        _logger.LogInformation("Bestätigungs-E-Mail an {ToEmail} gesendet.", toEmail);
    }
}
