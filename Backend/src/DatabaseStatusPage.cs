using System.Net;
using System.Text;

namespace Taetigkeitsbericht.Backend;

/// <summary>HTML-Statusseite für Datenbank-Erreichbarkeit (Browser, z. B. http://host:5108/).</summary>
internal static class DatabaseStatusPage
{
    public static string Render(
        bool reachable,
        string? error,
        bool useDsql,
        string? host,
        string database,
        string? migrationError = null,
        bool migrationsApplied = false)
    {
        var schemaOk = reachable && string.IsNullOrWhiteSpace(migrationError);
        var title = !reachable
            ? "Datenbank nicht erreichbar"
            : schemaOk
                ? "Datenbank erreichbar"
                : "Schema fehlt (Migration)";
        var tone = schemaOk ? "#0f7b3d" : "#b42318";
        var bg = schemaOk ? "#ecfdf3" : "#fef3f2";
        var detail = !reachable
            ? "Das Backend läuft, die Datenbank antwortet aber nicht. Bitte Verbindung, Cluster, IAM und Netzwerk (PrivateLink) prüfen."
            : schemaOk
                ? "Das Backend kann die Datenbank erreichen. GraphQL steht unter <code>/graphql</code> bereit."
                : "Verbindung zur Datenbank steht, aber die Tabellen wurden nicht angelegt. Migration läuft in DSQL als <code>admin</code>; die App-Rolle braucht danach DML-Rechte.";
        var combinedError = string.Join(
            "\n\n",
            new[] { error, migrationError }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var errorBlock = string.IsNullOrWhiteSpace(combinedError)
            ? ""
            : $"<pre>{WebUtility.HtmlEncode(combinedError)}</pre>";
        var migrateNote = migrationsApplied
            ? "Migrationen angewendet."
            : (string.IsNullOrWhiteSpace(migrationError) ? "" : "Migration fehlgeschlagen.");
        var target = useDsql
            ? $"Aurora DSQL · Host {WebUtility.HtmlEncode(host ?? "(nicht gesetzt)")} · Schema taetigkeitsbericht · Datenbank {WebUtility.HtmlEncode(database)}"
            : $"PostgreSQL · {WebUtility.HtmlEncode(host ?? "ConnectionStrings:DefaultConnection")} · Datenbank {WebUtility.HtmlEncode(database)}";
        if (!string.IsNullOrWhiteSpace(migrateNote))
        {
            target += " · " + migrateNote;
        }

        return $$"""
            <!DOCTYPE html>
            <html lang="de">
            <head>
              <meta charset="utf-8"/>
              <meta name="viewport" content="width=device-width, initial-scale=1"/>
              <title>Taetigkeitsbericht.Backend – {{WebUtility.HtmlEncode(title)}}</title>
              <style>
                body { font-family: system-ui, sans-serif; margin: 0; background: #f4f4f5; color: #18181b; }
                main { max-width: 40rem; margin: 3rem auto; padding: 1.5rem 1.75rem; background: #fff;
                       border-radius: 12px; box-shadow: 0 1px 4px rgba(0,0,0,.08); }
                .badge { display: inline-block; padding: .35rem .75rem; border-radius: 999px;
                         background: {{bg}}; color: {{tone}}; font-weight: 700; }
                h1 { font-size: 1.25rem; margin: 1rem 0 .5rem; }
                p, .meta { line-height: 1.5; color: #3f3f46; }
                .meta { font-size: .9rem; margin-top: 1.25rem; }
                pre { white-space: pre-wrap; word-break: break-word; background: #fafafa;
                      border: 1px solid #e4e4e7; border-radius: 8px; padding: .75rem 1rem; font-size: .85rem; }
                code { font-size: .9em; }
              </style>
            </head>
            <body>
              <main>
                <div class="badge">{{WebUtility.HtmlEncode(title)}}</div>
                <h1>Taetigkeitsbericht.Backend</h1>
                <p>{{detail}}</p>
                {{errorBlock}}
                <p class="meta">{{target}}</p>
              </main>
            </body>
            </html>
            """;
    }

    public static IResult ToResult(bool ready, string html) =>
        Results.Content(
            html,
            "text/html; charset=utf-8",
            Encoding.UTF8,
            ready ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
}
