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
        string database)
    {
        var title = reachable ? "Datenbank erreichbar" : "Datenbank nicht erreichbar";
        var tone = reachable ? "#0f7b3d" : "#b42318";
        var bg = reachable ? "#ecfdf3" : "#fef3f2";
        var detail = reachable
            ? "Das Backend kann die Datenbank erreichen. GraphQL steht unter <code>/graphql</code> bereit."
            : "Das Backend läuft, die Datenbank antwortet aber nicht. Bitte Verbindung, Cluster, IAM und Netzwerk (PrivateLink) prüfen.";
        var errorBlock = string.IsNullOrWhiteSpace(error)
            ? ""
            : $"<pre>{WebUtility.HtmlEncode(error)}</pre>";
        var target = useDsql
            ? $"Aurora DSQL · Host {WebUtility.HtmlEncode(host ?? "(nicht gesetzt)")} · Datenbank {WebUtility.HtmlEncode(database)}"
            : $"PostgreSQL · {WebUtility.HtmlEncode(host ?? "ConnectionStrings:DefaultConnection")} · Datenbank {WebUtility.HtmlEncode(database)}";

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

    public static IResult ToResult(bool reachable, string html) =>
        Results.Content(
            html,
            "text/html; charset=utf-8",
            Encoding.UTF8,
            reachable ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
}
