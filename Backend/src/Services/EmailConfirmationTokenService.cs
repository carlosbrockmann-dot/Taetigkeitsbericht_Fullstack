using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Taetigkeitsbericht.Backend.Services;

public class EmailConfirmationOptions
{
    public const string SectionName = "EmailConfirmation";

    /// <summary>Gültigkeit des Bestätigungstokens in Stunden.</summary>
    public int TokenExpiresHours { get; set; } = 24;

    /// <summary>Basis-URL für den Bestätigungslink (z. B. https://localhost:7022).</summary>
    public string ConfirmationBaseUrl { get; set; } = "https://localhost:7022";
}

public class EmailConfirmationTokenService : IEmailConfirmationTokenService
{
    private readonly EmailConfirmationOptions _options;

    public EmailConfirmationTokenService(IOptions<EmailConfirmationOptions> options)
    {
        _options = options.Value;
    }

    public string CreateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public DateTimeOffset CreateExpiryUtc()
        => DateTimeOffset.UtcNow.AddHours(_options.TokenExpiresHours);

    public bool IsValid(string? storedToken, DateTimeOffset? expiryUtc, string providedToken)
    {
        if (string.IsNullOrWhiteSpace(storedToken)
            || string.IsNullOrWhiteSpace(providedToken)
            || expiryUtc is null
            || expiryUtc <= DateTimeOffset.UtcNow
            || storedToken.Length != providedToken.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(storedToken),
            System.Text.Encoding.UTF8.GetBytes(providedToken));
    }
}
