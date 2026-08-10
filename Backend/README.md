# Tätigkeitsbericht – Backend

<strong style="color:blue">Geplantes C#-Backend mit GraphQL als Minimal API (noch nicht implementiert).</strong>

Eigenständige GraphQL-Applikation auf **ASP.NET Core Minimal API** zum Empfang und zur Speicherung hochgeladener Tätigkeitsberichte sowie zur Abfrage geleisteter Stunden. Authentifizierung per Login; bei gültigen Zugangsdaten wird ein Token ausgestellt und von Clients im HTTP-Header mitgeschickt.

<p style="color:red">Status: Planung – siehe <a href="../Planung.md">../Planung.md</a></p>

## Ziele

- Upload einer **Liste von Zeiteinträgen im JSON-Format** (von der Desktop-App)
- Persistenz in einer Tabelle analog Desktop-`Zeiteintrag`, ergänzt um **Mitarbeiter-ID**
- **GraphQL**-API mit **Hot Chocolate** (festgelegt)
- Schlanke Struktur im Stil einer **Minimal API** (kein Clean-Architecture-Schichtenmodell)
- Persistenz: **Npgsql** + **Entity Framework Core** (festgelegt)
- Datenbank: **On-Premises → PostgreSQL**; **AWS → Aurora DSQL** (festgelegt)
- **AWS-VPC:** Backend in der VPC; DB-Zugriff nur intern hinter Firewall/Security Groups
- Von **außen** erreichbare GraphQL-API (HTTPS) für Frontend und Desktop – nur nötige Freigaben
- Desktop-Clients: nach Login **Token** plus **TLS mit privaten Zertifikatsdateien** (mTLS)

## Geplante Technik

| Thema | Festgelegt |
|-------|------------|
| Laufzeit | .NET (ASP.NET Core **Minimal API**) |
| GraphQL | **Hot Chocolate** (`AddGraphQLServer`, `MapGraphQL`) |
| DB On-Premises | **PostgreSQL** (über **Npgsql** + EF Core) |
| DB AWS | **Aurora DSQL** (über **Npgsql** / kompatiblen Connection-String, EF Core) |
| Auth | JWT (o. ä.), Passwort-Hashing (z. B. PasswordHasher) |
| Struktur | Ein Web-Projekt (ggf. einfache Ordner: Models, Data, GraphQL) – **keine** Domain/Application/Infrastructure-Schichten |
| Tests | xUnit / NUnit, Integrationstests gegen Test-DB |

## Geplante Projektform (Minimal API)

Statt Clean Architecture: ein schlankes ASP.NET-Core-Projekt, in dem Konfiguration, EF Core und Hot-Chocolate-Endpunkte nahe beieinander liegen (typisch `Program.cs` + wenige Hilfsdateien).

| Bereich | Inhalt |
|---------|--------|
| **Host** | Minimal Hosting (`WebApplication.CreateBuilder`), DI, CORS, Auth, TLS |
| **Data** | EF-Core-`DbContext`, Entitäten `Mitarbeiter` / `Zeiteintrag`, Migrationen |
| **GraphQL** | Hot-Chocolate-Queries/Mutations (Login, Upload, Übersicht) |
| **Auth** | Login → JWT; geschützte Operationen per Header |

## Geplante API-Oberfläche (Skizze)

| Operation | Zweck |
|-----------|--------|
| Login | Benutzername + Passwort → Token |
| Upload Tätigkeitsbericht | JSON-Liste von Einträgen; Token im Header; Desktop zusätzlich private Zertifikate |
| Queries Übersicht | Geleistete Stunden (Filter Zeitraum / Mitarbeiter) |

## Datenbank und AWS-Netzwerk

| Umgebung | Datenbank |
|----------|-----------|
| **On-Premises** | **PostgreSQL** |
| **AWS** | **Aurora DSQL** |

- On-Premises und AWS teilen dasselbe Schema; Umschaltung über Connection String / `appsettings` (Profil `OnPremises` vs. `Aws`).
- **AWS:** Aurora DSQL in der **VPC**; Verbindung vom Backend nur über **interne** Security-Group-/Firewall-Regeln – **kein** öffentlicher DB-Zugang.
- Backend-API und (über Edge/ALB) das Frontend von außen für Benutzer freigeben (HTTPS).
- Kernentität Zeiteintrag: Felder analog Desktop (`datum`, Zeiten, Pausen, Anmerkung, …) plus **`mitarbeiter_id`**.

Orientierung Desktop-Modell: [../Desktop/readme_models.md](../Desktop/readme_models.md).  
Netzwerk-Zielbild und Aufgaben: [../Planung.md](../Planung.md) (AWS-Netzwerk, Phase 4).

## Abgrenzung

| Dieses Backend | Desktop | Frontend |
|----------------|---------|----------|
| Zentrale Speicherung und Auth (C# Minimal API + Hot Chocolate); VPC + DB-Firewall | Lokale Erfassung (Python, Clean Architecture); Upload mit Token + privaten Zertifikaten | React-Übersicht + Login; öffentlich in VPC erreichbar |

Das Backend bleibt **eigenständig** (eigene Solution/Projekt, keine Vermischung mit Desktop-SQLite).

## Nächste Schritte

Aufgaben und Reihenfolge: **[../Planung.md](../Planung.md)** (Phase 1, Phase 4 VPC). Setup-Anleitung (`dotnet new web`, Migrationen, Startbefehl) folgt mit der Implementierung.
