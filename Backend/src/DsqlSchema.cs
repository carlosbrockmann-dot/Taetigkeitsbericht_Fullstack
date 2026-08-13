using System.Text.RegularExpressions;
using Amazon.AuroraDsql.EntityFrameworkCore.Extensions;
using Amazon.AuroraDsql.Npgsql;
using Microsoft.EntityFrameworkCore;
using Taetigkeitsbericht.Backend.Data;

namespace Taetigkeitsbericht.Backend;

/// <summary>
/// Aurora DSQL: <c>public</c> ist eine System-Entität (kein GRANT darauf).
/// App-Objekte liegen im Schema <see cref="AppSchema"/>; Migrationen als admin,
/// DML als Rolle <c>verwaltung</c>.
/// </summary>
internal static class DsqlSchema
{
    public const string AppSchema = "taetigkeitsbericht";

    private static readonly Regex SafeRoleName = new(
        @"^[a-zA-Z_][a-zA-Z0-9_]*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static void ApplyConnectionDefaults(DsqlConfig cfg, DatabaseOptions options, string user)
    {
        cfg.User = user;
        cfg.Database = options.Database;
        cfg.Port = options.Port;
        cfg.OrmPrefix = "efcore";
        cfg.ConfigureConnectionString = cs => cs.SearchPath = AppSchema;
    }

    public static async Task ApplyMigrationsAndGrantsAsync(
        DatabaseOptions options,
        ILoggerFactory loggerFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var cfg = new DsqlConfig
        {
            Host = options.Host ?? throw new InvalidOperationException("Database:Host fehlt."),
        };
        ApplyConnectionDefaults(cfg, options, "admin");

        await using var adminSource = await DsqlDataSource.CreateAsync(cfg);
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseDsql(adminSource, loggerFactory, dsql => dsql.EnableIdentityColumns(65536))
            .Options;

        await using var db = new AppDbContext(dbOptions);

        // Schema/Rolle: Konstante bzw. Regex-geprüft (kein User-Input).
#pragma warning disable EF1003

        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "CREATE SCHEMA IF NOT EXISTS " + AppSchema,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "CREATE SCHEMA {Schema} übersprungen (existiert bereits?).", AppSchema);
        }

        var appRole = options.User;
        if (!string.IsNullOrWhiteSpace(appRole)
            && !appRole.Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            if (!SafeRoleName.IsMatch(appRole))
            {
                throw new InvalidOperationException($"Database:User ist kein gültiger Rollenname: {appRole}");
            }

            try
            {
                await db.Database.ExecuteSqlRawAsync(
                    "GRANT USAGE ON SCHEMA " + AppSchema + " TO " + appRole,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogInformation(ex, "GRANT USAGE auf {Schema} übersprungen.", AppSchema);
            }
            try
            {
                await db.Database.ExecuteSqlRawAsync(
                    "ALTER DEFAULT PRIVILEGES IN SCHEMA " + AppSchema
                    + " GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO " + appRole,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogInformation(ex, "ALTER DEFAULT PRIVILEGES nicht unterstützt – GRANT nach Migration.");
            }
        }

        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        logger.LogInformation(
            "DSQL-Migrationen als admin im Schema {Schema}. Ausstehend: {Count} ({Names})",
            AppSchema,
            pending.Count,
            pending.Count == 0 ? "keine" : string.Join(", ", pending));

        await db.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("DSQL-Migrationen abgeschlossen.");

        if (!string.IsNullOrWhiteSpace(appRole)
            && !appRole.Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            await db.Database.ExecuteSqlRawAsync(
                "GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA " + AppSchema + " TO " + appRole,
                cancellationToken);
            try
            {
                await db.Database.ExecuteSqlRawAsync(
                    "GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA " + AppSchema + " TO " + appRole,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogInformation(ex, "GRANT auf SEQUENCES übersprungen (in DSQL oft nicht nötig).");
            }

            logger.LogInformation("DML-Rechte auf Schema {Schema} an Rolle {Role} vergeben.", AppSchema, appRole);
        }
#pragma warning restore EF1003
    }
}
