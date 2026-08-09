# Tätigkeitsbericht

Monorepo für lokale Zeiterfassung, Server-Upload und Web-Übersicht.

<strong style="color:blue">Desktop ist ein Python-Projekt; Backend ebenfalls Python (geplant); Frontend React (geplant).</strong>

<p style="color:red">Backend und Frontend sind in Planung – siehe <a href="./Planung.md">Planung.md</a>. Implementierung steht aus.</p>

## Komponenten

| Ordner | Beschreibung |
|--------|----------------|
| **[Desktop/](./Desktop/)** | Vorhandene Desktop-App (Python, Clean Architecture, SQLite): Erfassung geleisteter Zeiten, Sollstunden, Urlaub/Krank/Feiertage, Excel-Export. Geplant: Login und Upload der Zeiteinträge als JSON-Liste an das Backend – mit Token und **privaten Zertifikatsdateien**. |
| **[Backend/](./Backend/)** | Geplante eigenständige GraphQL-API (Python, Clean Architecture, SOLID, ORM): Login mit Token im Header, Speicherung einer Zeiteintrags-Tabelle inkl. Mitarbeiter-ID; lokal PostgreSQL, in AWS Aurora DSQL. In AWS: **VPC**, DB nur intern hinter Firewall/Security Groups; API von außen freigegeben. |
| **[Frontend/](./Frontend/)** | Geplante React-App: Login und Übersicht der geleisteten Stunden über das Backend. In AWS: **VPC**, von außen für Benutzer erreichbar (HTTPS). |

Details und Aufgabenliste: **[Planung.md](./Planung.md)**.

## Kurzüberblick Architektur

```
Desktop (lokal, SQLite)
    │  Login → Token
    │  Upload JSON-Liste (GraphQL + Auth-Header + private Zertifikate / TLS)
    ▼
Internet → öffentliche Freigaben (HTTPS)
    ▼
┌────────────── AWS VPC ──────────────┐
│  Frontend (React)  Backend (GraphQL) │
│         │               │            │
│         └───────┬───────┘            │
│                 ▼                    │
│     Aurora DSQL (nur intern,         │
│     Firewall / Security Groups)      │
└──────────────────────────────────────┘
```

Die Server-Tabelle orientiert sich an der Desktop-`Zeiteintrag`-Struktur (`Desktop/readme_models.md`) und hält zusätzlich die **Mitarbeiter-ID** fest.

## AWS-Netzwerk (geplant)

- Backend und Frontend in einer **VPC**
- Kommunikation zur Datenbank **nur intern**, abgesichert per **Firewall / Security Groups**
- Frontend und Backend von **außen** für Benutzer mit den nötigen Port-/HTTPS-Freigaben
- Desktop: Zugang nach Login, zusätzlich mit **privaten Zertifikatsdateien**

Siehe Abschnitt „AWS-Netzwerk und Sicherheit“ in [Planung.md](./Planung.md).

## Weiterführende READMEs

- [Desktop/README.md](./Desktop/README.md) – Clean Architecture, Setup, Tests der Desktop-App  
- [Backend/README.md](./Backend/README.md) – geplantes Backend  
- [Frontend/README.md](./Frontend/README.md) – geplantes Frontend  
