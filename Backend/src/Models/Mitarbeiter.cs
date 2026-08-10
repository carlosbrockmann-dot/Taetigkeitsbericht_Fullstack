using System.ComponentModel.DataAnnotations;
using HotChocolate;

namespace Taetigkeitsbericht.Backend.Models;

/// <summary>
/// Mitarbeiter-Konto für Login und Zuordnung der Zeiteinträge.
/// </summary>
public class Mitarbeiter
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Benutzername { get; set; } = string.Empty;

    /// <summary>Passwort-Hash (nie Klartext speichern).</summary>
    [Required]
    [MaxLength(500)]
    [GraphQLIgnore]
    public string PasswortHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public bool EmailBestaetigt { get; set; }

    [MaxLength(200)]
    [GraphQLIgnore]
    public string? EmailBestaetigungsToken { get; set; }

    [GraphQLIgnore]
    public DateTimeOffset? EmailBestaetigungsTokenAblauf { get; set; }

    [GraphQLIgnore]
    public ICollection<Zeiteintrag> Zeiteintraege { get; set; } = new List<Zeiteintrag>();
}
