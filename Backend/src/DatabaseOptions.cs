namespace Taetigkeitsbericht.Backend;

/// <summary>
/// Datenbank-Anbindung: lokal PostgreSQL oder AWS Aurora DSQL (IAM-Tokens).
/// </summary>
public class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Wenn true: Aurora DSQL mit IAM-Auth (Amazon.AuroraDsql.*).</summary>
    public bool UseDsql { get; set; }

    /// <summary>DSQL-Cluster-Endpoint (Hostname).</summary>
    public string? Host { get; set; }

    public string User { get; set; } = "verwaltung";

    public string Database { get; set; } = "postgres";

    public int Port { get; set; } = 5432;

    /// <summary>Beim Start <c>Database.Migrate()</c> ausführen (Produktion/CI).</summary>
    public bool MigrateOnStartup { get; set; }
}
