namespace Taetigkeitsbericht.Backend;

/// <summary>Ergebnis der Start-Migration, für die HTML-Statusseite.</summary>
public sealed class DatabaseStartupState
{
    public bool MigrationsApplied { get; set; }

    public string? MigrationError { get; set; }
}
