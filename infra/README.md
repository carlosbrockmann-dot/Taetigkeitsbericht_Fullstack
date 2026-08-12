# Tätigkeitsbericht – AWS-Infrastruktur & CI/CD

Technische Kurzreferenz; **konkrete Schritte**: [../Deploy_aws.md](../Deploy_aws.md).

Zielbild: Bei Push auf `main` im Repo [Taetigkeitsbericht_Fullstack](https://github.com/carlosbrockmann-dot/Taetigkeitsbericht_Fullstack) werden Netzwerk (falls nötig), **Aurora DSQL** und zwei **EC2**-Instanzen (Backend / Frontend) angelegt oder aktualisiert. Der Datenbankverkehr läuft **privat** über die VPC (PrivateLink).

## Architektur

```
Internet
   │  HTTPS :80/:443 (Security Groups)
   ▼
┌──────────────────────── VPC 10.20.0.0/16 ────────────────────────┐
│  Public Subnets          Private Subnets                         │
│  ┌─────────────┐         ┌─────────────┐                         │
│  │ EC2 Frontend│         │ EC2 Backend │──:5432──► VPC Endpoint │
│  │ (Nginx+SPA) │         │ (.NET API)  │         (PrivateLink)  │
│  └─────────────┘         └─────────────┘              │          │
│                                                       ▼          │
│                                              Aurora DSQL         │
│                                         (kein öffentl. DB-Port)  │
└──────────────────────────────────────────────────────────────────┘
```

| Komponente | Rolle |
|------------|--------|
| VPC + Subnets | Öffentlich (App von außen), privat (nur intern / Endpoints) |
| EC2 Backend | ASP.NET GraphQL, IAM-Rolle für DSQL-Token |
| EC2 Frontend | Nginx liefert Vite-Build; Browser ruft Backend-URL auf |
| Aurora DSQL | PostgreSQL-kompatibel, Cluster + Interface-VPC-Endpoint |
| GitHub Actions | `deploy-aws.yml` bei Push auf `main` |

Offizielle Hinweise zu PrivateLink: [Managing Aurora DSQL with PrivateLink](https://docs.aws.amazon.com/aurora-dsql/latest/userguide/privatelink-managing-clusters.html).

## Wichtig: Aurora DSQL und Passwörter

**Aurora DSQL verwendet keine klassischen DB-Passwörter.** Authentifizierung läuft über **kurzlebige IAM-Tokens** ([Auth-Token](https://docs.aws.amazon.com/aurora-dsql/latest/userguide/SECTION_authentication-token.html), [Access control](https://aws.amazon.com/blogs/database/securing-amazon-aurora-dsql-access-control-best-practices/)).

| Wunsch | Realität bei Aurora DSQL |
|--------|---------------------------|
| Benutzer `verwaltung` | Möglich als **PostgreSQL-Rolle** `verwaltung` (`CREATE ROLE … WITH LOGIN`) |
| Passwort `verwaltung` | **Nicht** als festes Passwort – Verbindung mit IAM-Token als „Passwort“ |
| Datenbank `Taetigkeitsbericht` | Nach Cluster-Erstellung per SQL anlegen (soweit von DSQL unterstützt; sonst Schema/Tabellen in `postgres`) |

Die Pipeline legt die Rolle `verwaltung` an und verknüpft sie mit der EC2-IAM-Rolle (`AWS IAM GRANT`). Das Backend muss Tokens erzeugen (`aws dsql generate-db-connect-auth-token` bzw. AWS SDK) und als Connection-Password nutzen – **nicht** den Klartext `verwaltung` in Git committen.

Wenn Sie zwingend Benutzername+Passwort im Connection-String brauchen, ist **Amazon Aurora PostgreSQL** (nicht DSQL) die passendere Variante. DSQL bleibt aber das in `Planung.md` festgelegte AWS-Ziel.

## Dateien

| Pfad | Inhalt |
|------|--------|
| `.github/workflows/deploy-aws.yml` | Pipeline bei Push auf `main` (Access Keys) |
| `infra/cloudformation/taetigkeitsbericht-aws.yml` | VPC, SG, EC2, DSQL, PrivateLink-Endpoint |
| `infra/scripts/remote-bootstrap.sh` | User-Data / SSM: Runtime auf EC2 |
| `infra/scripts/post-dsql-setup.sql` | Rolle `verwaltung` + Hinweise DB-Name |

## GitHub Secrets (Repository → Settings → Secrets)

| Secret | Beispiel / Bedeutung |
|--------|----------------------|
| `AWS_ACCESS_KEY_ID` | Access Key ID des IAM-Deploy-Benutzers |
| `AWS_SECRET_ACCESS_KEY` | Secret Access Key (nur in GitHub Secrets) |
| `AWS_REGION` | z. B. `eu-central-1` (DSQL-Region prüfen) |
| `JWT_KEY` | langes Secret für Backend-JWT |
| `EC2_KEY_NAME` | optional, Name eines EC2-Key-Pairs (SSH); Deploy läuft primär über SSM |
| `BACKEND_HOST_PUBLIC` | nach erstem Deploy: öffentliche Backend-URL für Frontend-Build (`VITE_GRAPHQL_URL`) |

**Kein** `AWS_ROLE_ARN` / OIDC – die Pipeline nutzt Access Keys. Details: [Deploy_aws.md](../Deploy_aws.md).

**Keine DB-Passwörter** in Secrets speichern und in YAML hardcoden, solange DSQL IAM nutzt.

## Einmalig / manuell

1. AWS-Konto, Region mit Aurora DSQL.
2. GitHub Secrets setzen.
3. Optional EC2 Key Pair anlegen.
4. Push auf `main` → Workflow läuft.
5. Nach erstem erfolgreichen Stack: öffentliche Backend-URL in `BACKEND_HOST_PUBLIC` eintragen und erneut deployen (Frontend-Build).
6. `dotnet ef database update` auf dem Backend (oder Migrations-Job), sobald DSQL erreichbar ist.

## Lokal Stack testen

```powershell
aws cloudformation deploy `
  --stack-name taetigkeitsbericht `
  --template-file infra/cloudformation/taetigkeitsbericht-aws.yml `
  --capabilities CAPABILITY_NAMED_IAM `
  --parameter-overrides ProjectName=taetigkeitsbericht
```

## EC2 „erneuern“

Der Workflow aktualisiert den CloudFormation-Stack und führt danach per **SSM Run Command** ein App-Deploy aus (Artefakte aus dem Build). So bleibt die Instanz-ID oft gleich, die Software wird aber erneuert. Für kompletten Instance-Replace: Parameter `ForceInstanceRefresh=true` (ändert Launch-Template-UserData-Hash → Replacement).
