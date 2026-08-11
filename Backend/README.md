# Tätigkeitsbericht – Backend

ASP.NET-Core-API mit **Hot Chocolate** GraphQL zum Empfang und zur Speicherung hochgeladener Tätigkeitsberichte sowie zur Abfrage geleisteter Stunden. Authentifizierung per Login (JWT); Registrierung mit E-Mail-Bestätigung.

| | |
|--|--|
| Projektordner | `src/` |
| Target Framework | **net10.0** (siehe `Backend/global.json` für SDK-Pin) |
| Öffentliche API | **GraphQL** (`POST /graphql`) |
| REST | nur `GET /api/auth/confirm-email` (Link aus der Bestätigungs-Mail) |
| UI (Dev) | GraphiQL unter `/graphiql` – Banana Cake Pop ist deaktiviert |

## Technik

| Thema | Stand |
|-------|--------|
| Laufzeit | ASP.NET Core |
| API | Hot Chocolate GraphQL; Minimal-API nur für E-Mail-Confirm |
| Persistenz | EF Core + Npgsql |
| DB On-Premises | PostgreSQL |
| DB AWS | Aurora DSQL (geplant, gleicher Connection-String-Ansatz) |
| Auth | JWT Bearer, `PasswordHasher`, E-Mail-Bestätigungstoken |
| E-Mail | MailKit SMTP; bei `Smtp:Enabled=false` nur Log + Datei |

## Projektstruktur (kurz)

| Pfad | Inhalt |
|------|--------|
| `src/Program.cs` | DI, JWT, GraphQL, Confirm-GET, GraphiQL |
| `src/GraphQL/` | `Query`, `Mutation`, Payloads |
| `src/Services/` | Auth, JWT, E-Mail (Logging / SMTP) |
| `src/Repositories/`, `src/Data/` | EF Core, Migrationen |
| `src/Models/` | Entitäten und Request-DTOs |
| `src/wwwroot/graphiql/` | Statische GraphiQL-UI (Explorer-Plugin) |
| `src/appsettings*.json` | Nicht-geheime Konfiguration |

Keine Controllers-Schicht: Business-Logik über Services; Clients sprechen GraphQL.

## Voraussetzungen

- .NET SDK passend zu `global.json` / `net10.0`
- PostgreSQL lokal mit Datenbank `taetigkeitsbericht`
- Optional: `dotnet tool install --global dotnet-ef` für Migrationen

## Lokal starten

```powershell
cd Backend/src
dotnet ef database update
dotnet run --launch-profile http
```

- URL: **http://localhost:5108**
- GraphiQL: **http://localhost:5108/graphiql**
- Profil `http` setzt `ASPNETCORE_ENVIRONMENT=Development` (nötig für User Secrets und GraphiQL)

Connection String: `ConnectionStrings:DefaultConnection` in `appsettings.json`.

Nach Änderungen an Secrets oder `appsettings` **Backend neu starten** (Konfiguration wird nur beim Start geladen).

### Startup-Log E-Mail

Beim Start erscheint u. a.:

```text
E-Mail-Versand: Smtp.Enabled=True, Host=mail.gmx.net, From gesetzt=True, ...
```

Wenn `Smtp.Enabled=False` oder Zugangsdaten fehlen, wird **keine** echte Mail gesendet.

## API-Übersicht

| Endpoint / Operation | Auth | Zweck |
|----------------------|------|--------|
| `POST /graphql` → `register` | nein | Registrierung + Bestätigungs-E-Mail |
| `POST /graphql` → `login` | nein | Login → JWT (nur nach E-Mail-Bestätigung) |
| `POST /graphql` → `confirmEmail` | nein | E-Mail bestätigen (alternative zur GET-URL) |
| `POST /graphql` → `speichereZeiteintraege` | JWT | Zeiteinträge speichern |
| `POST /graphql` → Query `zeiteintraege` | JWT | Eigene Zeiteinträge (`von` / `bis` optional) |
| `GET /api/auth/confirm-email?token=` | nein | Bestätigungslink aus der Mail (Browser) |
| `GET /graphql?sdl` | nein | Schema als SDL (Hot Chocolate `EnableSchemaRequests`) |
| `GET /graphiql` | nein | GraphiQL (**nur Development**) |

### Auth-Ablauf

1. `register` → Mitarbeiter angelegt (`EmailBestaetigt=false`), Mail mit Link  
   Existiert Benutzername/E-Mail bereits und ist **noch nicht bestätigt**: neuer Token, erneute Mail an die **gespeicherte** E-Mail-Adresse (kein Fehler „bereits vergeben“).  
   Ist das Konto schon bestätigt: Fehler „bereits vergeben“.
2. Link öffnen (`/api/auth/confirm-email?token=…`) oder Mutation `confirmEmail`
3. `login` → JWT; Session wird in Tabelle `login_token` gespeichert (JTI + Token-Hash, Ablauf)
4. Geschützte Operationen: Header `Authorization: Bearer <token>` – Token muss in der DB aktiv sein
5. Neuer Login widerruft vorherige aktive Tokens desselben Mitarbeiters

In Development liefert `register` zusätzlich `confirmationLink` in der Antwort (auch wenn SMTP aktiv ist).  
Der Bestätigungslink wird bei jeder Registrierung / erneutem Versand zudem im **Konsolen-Log** ausgegeben (`E-Mail-Bestätigungslink für …`).

Tabelle `login_token`: `Id`, `MitarbeiterId`, `Jti`, `TokenHash` (SHA-256, kein Klartext-JWT), `ErstelltAm`, `LaeuftAbAm`, `WiderrufenAm`.

### Beispiel-Mutationen (GraphiQL)

```graphql
mutation Register($input: RegisterRequestInput!) {
  register(input: $input) {
    ok
    error
    mitarbeiterId
    email
    hinweis
    confirmationLink
  }
}
```

Variables:

```json
{
  "input": {
    "benutzername": "max",
    "passwort": "Geheim123!",
    "email": "max@example.com"
  }
}
```

```graphql
mutation Login($input: LoginRequestInput!) {
  login(input: $input) {
    ok
    error
    login { token expiresAt mitarbeiterId benutzername }
  }
}
```

JWT in GraphiQL: unten bei **Headers** z. B.

```json
{
  "Authorization": "Bearer EYER_TOKEN_HIER"
}
```

## GraphiQL

- Statische UI unter `wwwroot/graphiql` mit **Explorer-Plugin** (Schema-Baum links, Ordner-Icon)
- Schema-Übersicht braucht **Introspection** (in Development aktiv)
- Hot Chocolate Cost Analysis ist in Development abgeschwächt (`EnforceCostLimits=false`), sonst scheitern große Introspection-Queries oft und Docs/Explorer bleiben leer
- Banana Cake Pop / Nitro: `Tool.Enable = false`
- Hard-Refresh (`Ctrl+F5`) nach UI-Änderungen

## Konfiguration und Secrets

In `appsettings.json` / `appsettings.Development.json` stehen **keine** Passwörter. `Smtp:From`, `Smtp:UserName`, `Smtp:Password` bleiben dort leer.

| Schlüssel | Wo | Zweck |
|-----------|-----|--------|
| `ConnectionStrings:DefaultConnection` | appsettings | PostgreSQL |
| `Jwt:*` | appsettings (Prod: Secret Store) | Token-Signierung |
| `EmailConfirmation:ConfirmationBaseUrl` | appsettings | Basis-URL im Bestätigungslink (z. B. `http://localhost:5108`) |
| `EmailConfirmation:TokenExpiresHours` | appsettings | Gültigkeit des Tokens |
| `Smtp:Host`, `Port`, `EnableSsl` | appsettings (Defaults ok) | SMTP-Server (z. B. GMX `mail.gmx.net:587`) |
| `Smtp:Enabled`, `From`, `UserName`, `Password` | **User Secrets** / Env | Versand aktivieren + Zugangsdaten |

| Umgebung | Secrets ablegen |
|----------|-----------------|
| **Lokal (Development)** | [.NET User Secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) |
| **AWS / Produktion** | Secrets Manager / SSM → Umgebungsvariablen (`Smtp__Password`, `Smtp__Enabled`, …) |

Keine `.env`-Datei nötig: ASP.NET Core liest `.env` nicht von Haus aus. Empfohlen: User Secrets lokal, Env-Vars in der Cloud.

User Secrets sind im Projekt initialisiert (`UserSecretsId` in der `.csproj`).

### User Secrets setzen (SMTP, z. B. GMX)

```powershell
cd Backend/src
dotnet user-secrets set "Smtp:Enabled" "true"
dotnet user-secrets set "Smtp:From" "IHRE_ADRESSE@gmx.de"
dotnet user-secrets set "Smtp:UserName" "IHRE_ADRESSE@gmx.de"
dotnet user-secrets set "Smtp:Password" "IHR_PASSWORT"
```

Optional, falls Host/Port nicht in `appsettings` stehen:

```powershell
dotnet user-secrets set "Smtp:Host" "mail.gmx.net"
dotnet user-secrets set "Smtp:Port" "587"
dotnet user-secrets set "Smtp:EnableSsl" "true"
```

Nur Schlüssel auflisten (ohne Werte auszugeben):

```powershell
dotnet user-secrets list
```

### Umgebungsvariablen (z. B. AWS)

Doppelter Unterstrich = Nested Config:

```text
Smtp__Enabled=true
Smtp__From=...
Smtp__UserName=...
Smtp__Password=...
Smtp__Host=mail.gmx.net
Smtp__Port=587
```

## E-Mail-Bestätigung

| `Smtp:Enabled` | Verhalten |
|----------------|-----------|
| `false` (Default in appsettings) | `LoggingEmailSender`: Log + `src/logs/last-confirmation-email.txt`, **keine** Zustellung |
| `true` (+ vollständige Secrets) | `SmtpEmailSender` (MailKit): echte SMTP-Zustellung |

Fehler beim SMTP-Versand erscheinen in der GraphQL-Antwort / im Log (`E-Mail konnte nicht gesendet werden: …`).

### Typische Ursachen „keine Mail“

1. Backend nach dem Setzen der Secrets **nicht neu gestartet** (alter Prozess ohne SMTP)
2. Startup-Log zeigt `Smtp.Enabled=False` oder `Password gesetzt=False`
3. Spam-/Junk-Ordner
4. Absender (`From`) muss zum SMTP-Konto passen (bei GMX i. d. R. dieselbe Adresse wie `UserName`)
5. `ConfirmationBaseUrl` zeigt auf die falsche Host-URL → Link in der Mail funktioniert nicht, Mail kann trotzdem ankommen

## Datenbank und AWS-Netzwerk

| Umgebung | Datenbank |
|----------|-----------|
| **On-Premises** | PostgreSQL |
| **AWS** | Aurora DSQL (geplant) |

- Umschaltung über Connection String / Umgebung
- **AWS:** DB nur intern in der VPC (Security Groups) – kein öffentlicher DB-Zugang
- API von außen per HTTPS (z. B. über ALB) für Frontend und Desktop
- Introspection in Production standardmäßig aus (`DisableIntrospection`)

Orientierung Desktop-Modell: [../Desktop/readme_models.md](../Desktop/readme_models.md)  
Gesamtplanung: [../Planung.md](../Planung.md)

## Abgrenzung

| Backend | Desktop | Frontend |
|---------|---------|----------|
| Zentrale Speicherung und Auth; VPC + DB-Firewall | Lokale Erfassung (Python); Upload mit Token | React-Übersicht + Login |

Das Backend bleibt **eigenständig** (eigene Solution, keine Vermischung mit Desktop-SQLite).
