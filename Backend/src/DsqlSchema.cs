using System.Text.RegularExpressions;
using Amazon.AuroraDsql.EntityFrameworkCore.Extensions;
using Amazon.AuroraDsql.Npgsql;
using Microsoft.EntityFrameworkCore;
using Taetigkeitsbericht.Backend.Data;

namespace Taetigkeitsbericht.Backend;

/// <summary>
/// Aurora DSQL: DDL im Schema public nur als <c>admin</c>.
/// Die App-Rolle (verwaltung) bekommt danach DML-Rechte.
/// </summary>
internal static class DsqlSchema
{
    private static readonly Regex SafeRoleName = new(
        @"^[a-zA-Z_][a-zA-Z0-9_]*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static async Task ApplyMigrationsAndGrantsAsync(
        DatabaseOptions options,
        ILoggerFactory loggerFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await using var adminSource = await DsqlDataSource.CreateAsync(new DsqlConfig
        {
            Host = options.Host ?? throw new InvalidOperationException("Database:Host fehlt."),
            User = "admin",
            Database = options.Database,
            Port = options.Port,
            OrmPrefix = "efcore",
        });

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseDsql(adminSource, loggerFactory, dsql => dsql.EnableIdentityColumns(65536))
            .Options;

        await using var db = new AppDbContext(dbOptions);
        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        logger.LogInformation(
            "DSQL-Migrationen als admin. Ausstehend: {Count} ({Names})",
            pending.Count,
            pending.Count == 0 ? "keine" : string.Join(", ", pending));

        await db.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("DSQL-Migrationen abgeschlossen.");

        var appRole = options.User;
        if (string.IsNullOrWhiteSpace(appRole)
            || appRole.Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!SafeRoleName.IsMatch(appRole))
        {
            throw new InvalidOperationException($"Database:User ist kein gültiger Rollenname: {appRole}");
        }

        // Rollenname ist per Regex geprüft; GRANT akzeptiert keine Parameter.
        await db.Database.ExecuteSqlRawAsync(
            "GRANT USAGE ON SCHEMA public TO " + appRole,
            cancellationToken);
        await db.Database.ExecuteSqlRawAsync(
            "GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO " + appRole,
            cancellationToken);
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO " + appRole,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "GRANT auf SEQUENCES übersprungen (in DSQL oft nicht nötig).");
        }

        logger.LogInformation("DML-Rechte auf public an Rolle {Role} vergeben.", appRole);
    }
}
