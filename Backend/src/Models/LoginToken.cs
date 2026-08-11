using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HotChocolate;

namespace Taetigkeitsbericht.Backend.Models;

/// <summary>
/// Persistierte Login-Session inkl. JWT-Bezug (JTI + Token-Hash).
/// </summary>
public class LoginToken
{
    [Key]
    public Guid Id { get; set; }

    public int MitarbeiterId { get; set; }

    [ForeignKey(nameof(MitarbeiterId))]
    [GraphQLIgnore]
    public Mitarbeiter? Mitarbeiter { get; set; }

    /// <summary>JWT-ID (Claim <c>jti</c>) zur Zuordnung und Widerruf.</summary>
    [Required]
    [MaxLength(64)]
    public string Jti { get; set; } = string.Empty;

    /// <summary>SHA-256-Hash des JWT (kein Klartext in der DB).</summary>
    [Required]
    [MaxLength(64)]
    [GraphQLIgnore]
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ErstelltAm { get; set; }

    public DateTimeOffset LaeuftAbAm { get; set; }

    public DateTimeOffset? WiderrufenAm { get; set; }

    [NotMapped]
    public bool IstAktiv =>
        WiderrufenAm is null && LaeuftAbAm > DateTimeOffset.UtcNow;
}
