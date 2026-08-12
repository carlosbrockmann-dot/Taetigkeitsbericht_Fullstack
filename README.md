# Tätigkeitsbericht

Monorepo für lokale Zeiterfassung, Server-Upload und Web-Übersicht.

<strong style="color:blue">Desktop ist ein Python-Projekt; Backend C# / ASP.NET Core mit Hot Chocolate und Npgsql; Frontend React (Vite) für die Online-Monatsansicht.</strong>

<p>Backend und Desktop-Upload sind implementiert. Die React-Online-Ansicht liegt unter <a href="./Frontend/README.md">Frontend/</a> und wird vom Desktop per „Online ansehen“ geöffnet.</p>

## Komponenten

| Ordner | Beschreibung |
|--------|----------------|
| **[Desktop/](./Desktop/)** | Desktop-App (Python, Clean Architecture, SQLite): Erfassung, Excel-Export, **Abgeben** und **Online ansehen** gegen das Backend. |
| **[Backend/](./Backend/)** | GraphQL-API (C# / ASP.NET Core, Hot Chocolate, Npgsql): Login/JWT, Speichern und Abfragen von Zeiteinträgen. |
| **[Frontend/](./Frontend/)** | React-App (Vite): Monatsansicht der Zeiteinträge (nur Navigation, Token vom Desktop). |

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
│  Frontend (React)  Backend (C# Minimal API + GraphQL) │
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
- [Backend/README.md](./Backend/README.md) – GraphQL-Backend  
- [Frontend/README.md](./Frontend/README.md) – React Online-Ansicht  
- [infra/README.md](./infra/README.md) – AWS VPC, Aurora DSQL, EC2, GitHub Actions Deploy  
- [Deploy_aws.md](./Deploy_aws.md) – konkrete Schritte für den AWS-Deploy mit Access Keys (Desktop bleibt on premises)

## AWS-Deploy (Kurz)

Pipeline: Push auf `main` → [`.github/workflows/deploy-aws.yml`](./.github/workflows/deploy-aws.yml). Anleitung: [Deploy_aws.md](./Deploy_aws.md).

**IAM-Benutzer** (Access Keys für GitHub Actions):

| Sandbox / Test | Produktion |
|----------------|------------|
| Policy **`AdministratorAccess`** am Deploy-Benutzer | Least Privilege (CloudFormation, EC2, VPC, IAM, S3, SSM, DSQL, …) |

Ohne passende Rechte: `AccessDenied` in der Pipeline (z. B. CloudFormation).

**GitHub:** Secrets unter **Settings → Secrets and variables → Actions → Repository secrets**  
(`AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `AWS_REGION`, `JWT_KEY`).  
**Environment secrets** werden vom aktuellen Workflow **nicht** gelesen.
